using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace TAGWEBAPI.Models
{
	[Table("cart_items", Schema = "public")]
	public class CartItem
	{
		[Key]
		[Column("id")]
		public int Id { get; set; }

		[Required]
		[Column("cart_id")]
		public int CartId { get; set; }

		[Required]
		[Column("listing_id")]
		public int ListingId { get; set; }

		[Required]
		[Column("quantity")]
		public int Quantity { get; set; } = 1;

		[Column("added_at")]
		public DateTime AddedAt { get; set; } = DateTime.UtcNow;

		[ForeignKey("CartId")]
        [JsonIgnore]
        public Cart? Cart { get; set; }

		[ForeignKey("ListingId")]
		public Listing? Listing { get; set; }
	}
}