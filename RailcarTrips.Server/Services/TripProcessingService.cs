using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using RailcarTrips.Server.Data;
using RailcarTrips.Server.Data.Entities;
using RailcarTrips.Shared.DTOs;
using System.Globalization;

namespace RailcarTrips.Server.Services;

public class TripProcessingService : ITripProcessingService
{
    private readonly AppDbContext _context;
    private readonly ILogger<TripProcessingService> _logger;

    public TripProcessingService(AppDbContext context, ILogger<TripProcessingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<UploadResultDto> ProcessEventsFileAsync(Stream csvStream)
    {
        var result = new UploadResultDto();

        try
        {
            // Load reference data
            var cities = await _context.Cities
                .Include(c => c.TimeZone)
                .ToDictionaryAsync(c => c.CityId);

            var eventCodes = await _context.EventCodes
                .ToDictionaryAsync(ec => ec.Code);

            // Parse CSV
            using var reader = new StreamReader(csvStream);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim,
                MissingFieldFound = null
            });

            var records = new List<EventRecord>();
            await foreach (var record in csv.GetRecordsAsync<EventRecord>())
            {
                records.Add(record);
            }

            _logger.LogInformation("Parsed {Count} event records from CSV", records.Count);

            // Process events
            var processedEvents = new List<Event>();
            foreach (var record in records)
            {
                if (!cities.TryGetValue(record.CityId, out var city))
                {
                    result.Warnings.Add($"Unknown city_id: {record.CityId} for equipment {record.EquipmentId}");
                    continue;
                }

                if (!eventCodes.TryGetValue(record.EventCode, out var eventCode))
                {
                    result.Warnings.Add($"Unknown event_code: {record.EventCode} for equipment {record.EquipmentId}");
                    continue;
                }

                var eventTimeLocal = record.EventTime;
                var eventTimeUtc = ConvertToUtc(eventTimeLocal, city.TimeZone.UtcOffSet);

                var evt = new Event
                {
                    EquipmentId = record.EquipmentId,
                    EventCodeId = eventCode.EventCodeId,
                    EventTimeLocal = eventTimeLocal,
                    EventTimeUtc = eventTimeUtc,
                    CityId = city.CityId
                };

                processedEvents.Add(evt);
                result.EventsProcessed++;
            }

            // Group by equipment and sort by UTC time
            var groupedEvents = processedEvents
                .GroupBy(e => e.EquipmentId)
                .ToDictionary(g => g.Key, g => g.OrderBy(e => e.EventTimeUtc).ToList());

            // Process events into trips
            var trips = new List<Trip>();
            foreach (var (equipmentId, events) in groupedEvents)
            {
                Trip? currentTrip = null;

                foreach (var evt in events)
                {
                    var eventCode = eventCodes.Values.First(ec => ec.EventCodeId == evt.EventCodeId);

                    if (eventCode.Code == "W") // Released - start new trip
                    {
                        if (currentTrip != null)
                        {
                            // Close previous incomplete trip
                            currentTrip.IsComplete = false;
                            trips.Add(currentTrip);
                            result.Warnings.Add($"Incomplete trip for {equipmentId}: new W event before Z");
                        }

                        currentTrip = new Trip
                        {
                            EquipmentId = equipmentId,
                            OriginCityId = evt.CityId,
                            StartEventTime = evt.EventTimeUtc,
                            CreateTime = DateTime.UtcNow
                        };
                        evt.Trip = currentTrip;
                    }
                    else if (eventCode.Code == "Z") // Placed - end trip
                    {
                        if (currentTrip != null)
                        {
                            currentTrip.DestinationCityId = evt.CityId;
                            currentTrip.EndEventTime = evt.EventTimeUtc;
                            currentTrip.TotalTime = currentTrip.EndEventTime - currentTrip.StartEventTime;
                            currentTrip.IsComplete = true;
                            evt.Trip = currentTrip;
                            trips.Add(currentTrip);
                            currentTrip = null;
                        }
                        else
                        {
                            result.Warnings.Add($"Orphaned Z event for {equipmentId} at {evt.EventTimeUtc}");
                        }
                    }
                    else // Other events (A, D)
                    {
                        if (currentTrip != null)
                        {
                            evt.Trip = currentTrip;
                        }
                    }
                }

                // Handle any remaining open trip
                if (currentTrip != null)
                {
                    currentTrip.IsComplete = false;
                    trips.Add(currentTrip);
                }
            }

            // Save to database
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Trips.AddRange(trips);
                await _context.SaveChangesAsync();

                // Now add events with trip IDs set
                _context.Events.AddRange(processedEvents);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                result.TripsCreated = trips.Count;
                result.SuccessCount = result.EventsProcessed;
                _logger.LogInformation("Created {TripCount} trips from {EventCount} events",
                    trips.Count, result.EventsProcessed);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to save trips and events");
                result.Errors.Add($"Database error: {ex.Message}");
                result.ErrorCount++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process events file");
            result.Errors.Add($"Processing error: {ex.Message}");
            result.ErrorCount++;
        }

        return result;
    }

    private static DateTime ConvertToUtc(DateTime localTime, string utcOffset)
    {
        // Parse offset like "-05:00" or "+08:00"
        var sign = utcOffset[0] == '-' ? -1 : 1;
        var parts = utcOffset.TrimStart('+', '-').Split(':');
        var hours = int.Parse(parts[0]);
        var minutes = parts.Length > 1 ? int.Parse(parts[1]) : 0;
        var offset = new TimeSpan(sign * hours, sign * minutes, 0);

        // UTC = Local - Offset
        return localTime - offset;
    }

    private class EventRecord
    {
        [CsvHelper.Configuration.Attributes.Name("Equipment Id")]
        public string EquipmentId { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("Event Code")]
        public string EventCode { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("City Id")]
        public int CityId { get; set; }

        [CsvHelper.Configuration.Attributes.Name("Event Time")]
        public DateTime EventTime { get; set; }
    }
}
