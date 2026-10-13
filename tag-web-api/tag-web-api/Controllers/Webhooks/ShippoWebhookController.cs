using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Threading.Tasks;
using TAGWEBAPI.Data;
using TAGWEBAPI.Integrations.ModernTreasury;

namespace TAGWEBAPI.Controllers.Webhooks
{
    [ApiController]
    [Route("api/webhooks/shippo")]
    [Authorize]
    public class ShippoWebhookController : ControllerBase
    {
        private readonly TAGDBContext _context;
        private readonly IModernTreasuryLedgerService _ledgerService;

        public ShippoWebhookController(TAGDBContext context, IModernTreasuryLedgerService ledgerService)
        {
            _context = context;
            _ledgerService = ledgerService;
        }

        // Shippo's payload structure
        public class ShippoWebhookPayload
        {
            public string Event { get; set; } = string.Empty;
            public ShippoWebhookData? Data { get; set; }
        }

        public class ShippoWebhookData
        {
            public string TrackingNumber { get; set; } = string.Empty;
            public ShippoTrackingStatus? TrackingStatus { get; set; }
        }

        public class ShippoTrackingStatus
        {
            public string Status { get; set; } = string.Empty; // e.g., "TRANSIT", "DELIVERED", "RETURNED", "FAILURE"
        }

        [HttpPost]
        public async Task<IActionResult> HandleShippoWebhook([FromBody] JsonElement rawPayload)
        {
            // 1. Safely parse the Shippo payload
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true };
            var payload = JsonSerializer.Deserialize<ShippoWebhookPayload>(rawPayload.GetRawText(), options);

            // We only care about tracking updates
            if (payload?.Event != "track_updated" || payload.Data == null)
            {
                return Ok(); // Acknowledge receipt so Shippo doesn't retry, but ignore it.
            }

            string trackingNumber = payload.Data.TrackingNumber;
            string newStatus = payload.Data.TrackingStatus?.Status?.ToUpperInvariant() ?? "";

            if (string.IsNullOrEmpty(trackingNumber)) 
                return Ok();

            // 2. Find the associated Order internally
            var order = await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Listing)
                .FirstOrDefaultAsync(o => o.TrackingNumber == trackingNumber);

            if (order == null) 
                return Ok(); // Tracking number doesn't belong to our system

            // 3. Process Package Delivery
            if (newStatus == "DELIVERED" && order.Status != "Delivered")
            {
                // Mark as securely delivered!
                order.Status = "Delivered";

                Console.WriteLine($"[PAYOUT TRIGGERED] Order {order.OrderNumber} delivered. Releasing funds to Artist via ACH...");

                // EXECUTING THE REAL ROUTE
                await _ledgerService.ExecuteArtistACHPayoutAsync(order.Id);

                await _context.SaveChangesAsync();
            }
            else if (newStatus == "RETURNED" || newStatus == "FAILURE")
            {
                 order.Status = "Delivery Failed";
                 await _context.SaveChangesAsync();
            }

            return Ok();
        }
    }
}