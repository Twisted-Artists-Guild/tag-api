using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models
{
    [Table("carts", Schema = "public")]
    public class Cart
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; } // Nullable for guest carts (can use session ID)

        [Column("session_id")]
        public string? SessionId { get; set; } // For guest tracking

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<CartItem> Items { get; set; } = new List<CartItem>();

        [ForeignKey("UserId")]
        public NextAuthUser? User { get; set; }
    }
}