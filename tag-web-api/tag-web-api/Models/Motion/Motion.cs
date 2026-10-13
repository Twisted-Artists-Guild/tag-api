using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace TAGWEBAPI.Models
{
    [Table("motions", Schema = "public")]
    public class Motion
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Column("slug")]
        public string Slug { get; set; } = string.Empty;

        [Required]
        [Column("subject")]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Column("attachments")]
        public string? Attachments { get; set; }

        [Required]
        [Column("proposed_by")]
        public int ProposedById { get; set; }

        [Required]
        [Column("proposed_on")]
        public DateTime ProposedOn { get; set; } = DateTime.UtcNow;

        [Column("seconded_by")]
        public int? SecondedById { get; set; }

        [Column("seconded_on")]
        public DateTime? SecondedOn { get; set; }

        [Required]
        [Column("duration")]
        public string Duration { get; set; } = string.Empty;

        [Column("status")]
        public string Status { get; set; } = "Proposed"; // Proposed, Seconded, Closed

        // Navigation parameters
        [ForeignKey("ProposedById")]
        public NextAuthUser? ProposedBy { get; set; }

        [ForeignKey("SecondedById")]
        public NextAuthUser? SecondedBy { get; set; }

        public ICollection<MotionVote> Votes { get; set; } = new List<MotionVote>();
    }
}