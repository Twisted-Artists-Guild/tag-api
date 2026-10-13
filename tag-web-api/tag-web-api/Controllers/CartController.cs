using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TAGWEBAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CartController : ControllerBase
    {
        private readonly TAGDBContext _context;

        public CartController(TAGDBContext context)
        {
            _context = context;
        }

        // 1. GET: Fetch the user's active cart
        [HttpGet]
        public async Task<IActionResult> GetCart([FromQuery] int userId)
        {
            var cart = await _context.Carts
                .Include(c => c.Items)
                    .ThenInclude(i => i.Listing)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            // If user has no cart, send an empty response, don't create one until they add an item.
            if (cart == null)
            {
                return Ok(new { Items = new List<CartItem>() });
            }

            return Ok(cart);
        }

        public class AddItemRequest { public int UserId { get; set; } public int ListingId { get; set; } public int Quantity { get; set; } }

        // 2. POST: Add item to cart
        [HttpPost("add")]
        public async Task<IActionResult> AddToCart([FromBody] AddItemRequest request)
        {
            // Find or create cart
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == request.UserId);

            if (cart == null)
            {
                cart = new Cart { UserId = request.UserId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            // Check if item already in cart
            var existingItem = cart.Items.FirstOrDefault(i => i.ListingId == request.ListingId);

            if (existingItem != null)
            {
                existingItem.Quantity += request.Quantity;
            }
            else
            {
                var newItem = new CartItem 
                { 
                    CartId = cart.Id, 
                    ListingId = request.ListingId, 
                    Quantity = request.Quantity 
                };
                _context.CartItems.Add(newItem);
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok();
        }

        public class UpdateItemRequest { public int UserId { get; set; } public int ListingId { get; set; } public int Quantity { get; set; } }

        // 3. PUT: Update item quantity
        [HttpPut("update")]
        public async Task<IActionResult> UpdateQuantity([FromBody] UpdateItemRequest request)
        {
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == request.UserId);

            if (cart == null) return NotFound("Cart not found");

            var item = cart.Items.FirstOrDefault(i => i.ListingId == request.ListingId);
            if (item != null)
            {
                if (request.Quantity <= 0)
                {
                    _context.CartItems.Remove(item);
                }
                else
                {
                    item.Quantity = request.Quantity;
                }

                cart.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return Ok();
        }

        public class RemoveItemRequest { public int UserId { get; set; } public int ListingId { get; set; } }

        // 4. DELETE: Remove item
        [HttpDelete("remove")]
        public async Task<IActionResult> RemoveFromCart([FromBody] RemoveItemRequest request)
        {
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == request.UserId);

            if (cart == null) return NotFound("Cart not found");

            var item = cart.Items.FirstOrDefault(i => i.ListingId == request.ListingId);
            if (item != null)
            {
                _context.CartItems.Remove(item);
                cart.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return Ok();
        }
    }
}