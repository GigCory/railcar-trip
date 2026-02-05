using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RailcarTrips.Server.Data.Entities;

[Table("Cities")]
public class City
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int CityId { get; set; }

    [Required]
    [MaxLength(100)]
    public string CityName { get; set; } = string.Empty;

    public int TimeZoneId { get; set; }

    [ForeignKey(nameof(TimeZoneId))]
    public AppTimeZone TimeZone { get; set; } = null!;

    public ICollection<Event> Events { get; set; } = new List<Event>();
}
