using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RailcarTrips.Server.Data.Entities;

[Table("Trips")]
public class Trip
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int TripId { get; set; }

    [Required]
    [MaxLength(50)]
    public string EquipmentId { get; set; } = string.Empty;

    public int? OriginCityId { get; set; }

    public int? DestinationCityId { get; set; }

    public DateTime? StartEventTime { get; set; }

    public DateTime? EndEventTime { get; set; }

    public TimeSpan? TotalTime { get; set; }

    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    public bool IsComplete { get; set; } = false;

    [ForeignKey(nameof(OriginCityId))]
    public City? OriginCity { get; set; }

    [ForeignKey(nameof(DestinationCityId))]
    public City? DestinationCity { get; set; }

    public ICollection<Event> Events { get; set; } = new List<Event>();
}
