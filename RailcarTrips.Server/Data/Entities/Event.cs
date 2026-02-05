using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RailcarTrips.Server.Data.Entities;

[Table("Events")]
public class Event
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int EventId { get; set; }

    [Required]
    [MaxLength(50)]
    public string EquipmentId { get; set; } = string.Empty;

    public int EventCodeId { get; set; }

    public DateTime EventTimeUtc { get; set; }

    public DateTime EventTimeLocal { get; set; }

    public int CityId { get; set; }

    public int? TripId { get; set; }

    [ForeignKey(nameof(EventCodeId))]
    public EventCode EventCode { get; set; } = null!;

    [ForeignKey(nameof(CityId))]
    public City City { get; set; } = null!;

    [ForeignKey(nameof(TripId))]
    public Trip? Trip { get; set; }
}
