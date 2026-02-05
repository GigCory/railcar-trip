using RailcarTrips.Shared.DTOs;

namespace RailcarTrips.Server.Services;

public interface ITripProcessingService
{
    Task<UploadResultDto> ProcessEventsFileAsync(Stream csvStream);
}
