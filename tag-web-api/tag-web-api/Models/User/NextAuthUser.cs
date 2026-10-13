using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

[Table("users", Schema = "public")]
public class NextAuthUser
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("email")]
    public string? Email { get; set; }

    [Column("emailVerified")]
    public DateTime? EmailVerified { get; set; }

    [Column("image")]
    public string? Image { get; set; }
}