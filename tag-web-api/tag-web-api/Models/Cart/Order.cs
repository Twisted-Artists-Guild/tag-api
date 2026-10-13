using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace TAGWEBAPI.Models
{
    [Table("orders", Schema = "public")]
    public class Order
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        [Required]
        [Column("order_number")]
        public string OrderNumber { get; set; } = string.Empty;

        [Column("stripe_payment_intent_id")]
        public string? StripePaymentIntentId { get; set; }

        [Required]
        [Column("total_cents")]
        public int TotalCents { get; set; }

        [Column("status")]
        public string Status { get; set; } = "Processing"; // Processing, Shipped, Delivered, Refunded

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // --- NEW SHIPPO FIELDS ---
        [Column("shipping_label_url")]
        public string? ShippingLabelUrl { get; set; }

        [Column("tracking_number")]
        public string? TrackingNumber { get; set; }

        [ForeignKey("UserId")]
        [JsonIgnore]
        public NextAuthUser? User { get; set; }

        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    }

    [Table("order_items", Schema = "public")]
    public class OrderItem
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("order_id")]
        public int OrderId { get; set; }

        [Required]
        [Column("listing_id")]
        public int ListingId { get; set; }

        [Required]
        [Column("quantity")]
        public int Quantity { get; set; }

        [Required]
        [Column("unit_price_cents")]
        public int UnitPriceCents { get; set; }

        [ForeignKey("OrderId")]
        [JsonIgnore]
        public Order? Order { get; set; }

        [ForeignKey("ListingId")]
        public Listing? Listing { get; set; }
    }
}