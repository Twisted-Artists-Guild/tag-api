using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;
using TAGWEBAPI.Integrations.ModernTreasury;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;

namespace TAGWEBAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly TAGDBContext _context;
        private readonly IModernTreasuryLedgerService _ledgerService;

        public OrderController(TAGDBContext context, IModernTreasuryLedgerService ledgerService)
        {
            _context = context;
            _ledgerService = ledgerService;
        }

        // GET: api/order/user/5
        // Get all orders grouped for a specific buyer
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserOrders(int userId)
        {
            var orders = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Listing)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return Ok(orders);
        }

        // GET: api/order/artist/10
        // Get all orders that include at least one item from the given artist
        [HttpGet("artist/{artistId}")]
        public async Task<IActionResult> GetArtistOrders(int artistId)
        {
            var orders = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Listing)
                .Where(o => o.Items.Any(i => i.Listing != null && i.Listing.ArtistID == artistId))
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new {
                    o.Id,
                    o.OrderNumber,
                    o.CreatedAt,
                    o.Status,
                    // Filter the returned items so the Artist ONLY sees their own items from the order
                    ArtistItems = o.Items.Where(i => i.Listing != null && i.Listing.ArtistID == artistId).Select(i => new {
                        i.Id,
                        i.ListingId,
                        i.Quantity,
                        i.UnitPriceCents,
                        ListingTitle = i.Listing != null ? i.Listing.Title : "Unknown",
                        ImageUrl = i.Listing != null ? (i.Listing.CoverPic != null ? i.Listing.CoverPic.NormalizedURL : "") : ""
                    }),
                    // Compute a subtotal specifically for this artist's cut
                    ArtistSubtotalCents = o.Items.Where(i => i.Listing != null && i.Listing.ArtistID == artistId)
                                                 .Sum(i => i.Quantity * i.UnitPriceCents)
                })
                .ToListAsync();

            return Ok(orders);
        }

        // GET: api/order/5
        // Get a specific order containing full line item details (Securely handled based on caller)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrderDetails(int id, [FromQuery] int? userId, [FromQuery] int? artistId)
        {
            if (userId == null && artistId == null) return BadRequest("Must provide userId or artistId to auth.");

            var query = _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Listing)
                        .ThenInclude(l => l.Artist)
                .Where(o => o.Id == id);

            if (userId.HasValue) 
            {
                query = query.Where(o => o.UserId == userId.Value);
            }
            
            if (artistId.HasValue)
            {
                query = query.Where(o => o.Items.Any(i => i.Listing != null && i.Listing.ArtistID == artistId.Value));
            }

            var order = await query.FirstOrDefaultAsync();

            if (order == null) return NotFound("Order not found or access denied.");

            return Ok(order);
        }

        public class PlaceOrderRequest 
        { 
            public int UserId { get; set; } 
            public string PaymentIntentId { get; set; } = string.Empty;
            public string? ShippingLabelUrl { get; set; }
            public string? TrackingNumber { get; set; }
        }

        [HttpPost("place-order")]
        public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest request)
        {
            // 1. Fetch Cart
            var cart = await _context.Carts
                .Include(c => c.Items)
                .ThenInclude(i => i.Listing)
                .FirstOrDefaultAsync(c => c.UserId == request.UserId);

            if (cart == null || !cart.Items.Any())
                return BadRequest("Cart is empty or not found.");

            // 2. Separate Items by Artist to match Treasury payload expectations
            // In a real multi-vendor setup, you would create one Treasury Ledger request per Seller.
            var itemsByArtist = cart.Items.GroupBy(i => i.Listing?.ArtistID ?? 0).ToList();

            int totalOrderCents = 0;
            var orderItems = new List<OrderItem>();

            foreach (var item in cart.Items)
            {
                if (item.Listing == null) continue;

                int priceCents = (int)Math.Round((item.Listing.Price ?? 0) * 100);
                totalOrderCents += priceCents * item.Quantity;

                orderItems.Add(new OrderItem
                {
                    ListingId = item.ListingId,
                    Quantity = item.Quantity,
                    UnitPriceCents = priceCents
                });
            }

            // 3. Create the Database Order 
            string randomHash = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
            var orderNumber = $"TAG-{randomHash}";

            var order = new Order
            {
                UserId = request.UserId,
                OrderNumber = orderNumber,
                StripePaymentIntentId = request.PaymentIntentId,
                TotalCents = totalOrderCents,
                Status = "Processing",
                ShippingLabelUrl = request.ShippingLabelUrl, // <-- SAVES LABEL
                TrackingNumber = request.TrackingNumber,     // <-- SAVES TRACKING
                Items = orderItems
            };

            _context.Orders.Add(order);

            // 4. Empty the Cart
            _context.CartItems.RemoveRange(cart.Items);

            // 5. Save all DB changes atomically first (Ensure receipt is generated before ledgering)
            await _context.SaveChangesAsync();


            // 6. Push Ledger Events to Modern Treasury
            // Note: Loops through unique sellers and calculates ledger payouts dynamically using platform configuration constraints.
            try
            {
                foreach (var artistGroup in itemsByArtist)
                {
                    int artistGrossCents = 0;
                    foreach(var groupedItem in artistGroup)
                    {
                        artistGrossCents += (int)Math.Round((groupedItem.Listing?.Price ?? 0) * 100) * groupedItem.Quantity;
                    }

                    // Configuration assumptions (Aligning with your UI testing default constants)
                    int platformFee = (int)Math.Round(artistGrossCents * 0.065); // 6.5% platform fee
                    int stripeFee = (int)Math.Round(artistGrossCents * 0.029) + 30; // 2.9% + 30 cents per artist group
                    
                    var ledgerRequest = new StripeEventAccountingRequest
                    {
                        StripeEventId = $"{request.PaymentIntentId}_a{artistGroup.Key}",
                        StripeEventType = "payment_intent.succeeded",
                        StripePaymentIntentId = request.PaymentIntentId,
                        OrderId = orderNumber,
                        
                        AmountTotalCents = artistGrossCents,
                        PlatformFeeCents = platformFee,
                        StripeFeeCents = stripeFee,
                        
                        // Default shipping and tax assumptions right now (Shippo integrations usually append via webhooks later)
                        AmountTaxCents = 0, 
                        AmountShippingCents = 0, 
                        ShippingCostCents = 0,

                        SellerType = "artist",
                        Currency = "USD",
                        DryRun = false, 
                        
                        BuyerUserId = request.UserId.ToString(),
                        SellerAccountId = artistGroup.Key.ToString(),
                        Description = $"Purchase of {artistGroup.Sum(i => i.Quantity)} item(s) from Order #{orderNumber}"
                    };

                    await _ledgerService.PostStripeEventAsync(ledgerRequest, default);
                }
            }
            catch (Exception ex)
            {
                // We do NOT abort the function or send a 500 error here. 
                // The DB saved the physical order perfectly in Step 5. 
                // If treasury crashes (rate limits, wrong API key, network error), the user still gets their successfully checked out page!
                Console.WriteLine($"[FATAL] Modern Treasury Sync Failed for Order {orderNumber}: {ex.Message}");
            }

            return Ok(new { 
                OrderId = order.Id, 
                OrderNumber = order.OrderNumber,
                TotalCents = order.TotalCents,
                Status = order.Status
            });
        }

        [HttpPost("{id}/ship")]
        public async Task<IActionResult> MarkOrderAsShipped(int id, [FromQuery] int artistId)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Listing)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound("Order not found.");

            // Security: Ensure this artist actually sold an item in this order
            if (!order.Items.Any(i => i.Listing != null && i.Listing.ArtistID == artistId))
            {
                return Unauthorized("You are not authorized to update this order.");
            }

            order.Status = "Shipped";
            await _context.SaveChangesAsync();

            return Ok(new { order.Id, order.Status });
        }
    }
}