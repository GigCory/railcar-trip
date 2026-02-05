using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RailcarTrips.Server.Data;
using RailcarTrips.Server.Services;
using RailcarTrips.Shared.DTOs;

namespace RailcarTrips.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
//[Authorize] can use JWT or API key to authenticate
public class TripsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITripProcessingService _tripProcessingService;
    private readonly ILogger<TripsController> _logger;

    public TripsController(
        AppDbContext context,
        ITripProcessingService tripProcessingService,
        ILogger<TripsController> logger)
    {
        _context = context;
        _tripProcessingService = tripProcessingService;
        _logger = logger;
    }

    /// <summary>
    /// Upload and process equipment events CSV file
    /// </summary>
    [HttpPost("upload")]
    public async Task<ActionResult<UploadResultDto>> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new UploadResultDto
            {
                Errors = new List<string> { "No file uploaded" }
            });
        }

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new UploadResultDto
            {
                Errors = new List<string> { "File must be a CSV file" }
            });
        }

        _logger.LogInformation("Processing uploaded file: {FileName}", file.FileName);

        using var stream = file.OpenReadStream();
        var result = await _tripProcessingService.ProcessEventsFileAsync(stream);

        return Ok(result);
    }

    /// <summary>
    /// Get all trips
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<TripDto>>> GetTrips()
    {
        var trips = await _context.Trips
            .Include(t => t.OriginCity)
            .Include(t => t.DestinationCity)
            .OrderByDescending(t => t.StartEventTime)
            .Select(t => new TripDto
            {
                TripId = t.TripId,
                EquipmentId = t.EquipmentId,
                Origin = t.OriginCity != null ? t.OriginCity.CityName : "Unknown",
                Destination = t.DestinationCity != null ? t.DestinationCity.CityName : "In Transit",
                StartDateTime = t.StartEventTime,
                EndDateTime = t.EndEventTime,
                TotalTripHours = t.TotalTime.HasValue ? t.TotalTime.Value.TotalHours : null,
                IsComplete = t.IsComplete
            })
            .ToListAsync();

        return Ok(trips);
    }

    /// <summary>
    /// Get trip details with events
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TripWithEventsDto>> GetTrip(int id)
    {
        var trip = await _context.Trips
            .Include(t => t.OriginCity)
            .Include(t => t.DestinationCity)
            .Include(t => t.Events)
                .ThenInclude(e => e.City)
            .Include(t => t.Events)
                .ThenInclude(e => e.EventCode)
            .FirstOrDefaultAsync(t => t.TripId == id);

        if (trip == null)
        {
            return NotFound();
        }

        var dto = new TripWithEventsDto
        {
            TripId = trip.TripId,
            EquipmentId = trip.EquipmentId,
            Origin = trip.OriginCity?.CityName ?? "Unknown",
            Destination = trip.DestinationCity?.CityName ?? "In Transit",
            StartDateTime = trip.StartEventTime,
            EndDateTime = trip.EndEventTime,
            TotalTripHours = trip.TotalTime?.TotalHours,
            IsComplete = trip.IsComplete,
            Events = trip.Events
                .OrderBy(e => e.EventTimeUtc)
                .Select(e => new EventDto
                {
                    EventId = e.EventId,
                    EquipmentId = e.EquipmentId,
                    EventCode = e.EventCode.Code,
                    EventDescription = e.EventCode.Description,
                    CityName = e.City.CityName,
                    EventTimeLocal = e.EventTimeLocal,
                    EventTimeUtc = e.EventTimeUtc
                })
                .ToList()
        };

        return Ok(dto);
    }
}
