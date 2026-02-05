namespace RailcarTrips.Shared.DTOs;

public class EventDto
{
    public int EventId { get; set; }
    public string EquipmentId { get; set; } = string.Empty;
    public string EventCode { get; set; } = string.Empty;
    public string EventDescription { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    public DateTime EventTimeLocal { get; set; }
    public DateTime EventTimeUtc { get; set; }
}
