using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RailcarTrips.Server.Data.Entities;

[Table("TimeZones")]
public class AppTimeZone
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int TimeZoneId { get; set; }

    [Required]
    [MaxLength(100)]
    public string TimeZoneName { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string UtcOffSet { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<City> Cities { get; set; } = new List<City>();
}
