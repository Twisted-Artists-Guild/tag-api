using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace TAGWEBAPI.Models
{
    [Table("motion_votes", Schema = "public")]
    public class MotionVote
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("motion_id")]
        public int MotionId { get; set; }

        [Required]
        [Column("voter_id")]
        public int VoterId { get; set; }

        [Required]
        [Column("vote_value")]
        public string VoteValue { get; set; } = string.Empty; // "Yes", "No", "Abstain"

        [Column("voted_on")]
        public DateTime VotedOn { get; set; } = DateTime.UtcNow;

        [ForeignKey("MotionId")]
        [JsonIgnore]
        public Motion? Motion { get; set; }

        [ForeignKey("VoterId")]
        public NextAuthUser? Voter { get; set; }
    }
}