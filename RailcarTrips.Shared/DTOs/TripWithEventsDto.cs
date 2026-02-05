namespace RailcarTrips.Shared.DTOs;

public class TripWithEventsDto
{
    public int TripId { get; set; }
    public string EquipmentId { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime? StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public double? TotalTripHours { get; set; }
    public bool IsComplete { get; set; }
    public List<EventDto> Events { get; set; } = new();
}
