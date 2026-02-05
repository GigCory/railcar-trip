using RailcarTrips.Shared.DTOs;

namespace RailcarTrips.Client.Services;

public interface ITripService
{
    Task<UploadResultDto> UploadEventsAsync(Stream fileStream, string fileName);
    Task<List<TripDto>> GetTripsAsync();
    Task<TripWithEventsDto?> GetTripDetailsAsync(int tripId);
}
