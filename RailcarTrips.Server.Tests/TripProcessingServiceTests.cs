using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using RailcarTrips.Server.Data;
using RailcarTrips.Server.Data.Entities;
using RailcarTrips.Server.Services;
using System.Text;

namespace RailcarTrips.Server.Tests;

[TestFixture]
public class TripProcessingServiceTests
{
    private AppDbContext _context = null!;
    private Mock<ILogger<TripProcessingService>> _loggerMock = null!;
    private TripProcessingService _service = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new AppDbContext(options);
        _loggerMock = new Mock<ILogger<TripProcessingService>>();
        _service = new TripProcessingService(_context, _loggerMock.Object);

        SeedReferenceData();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    private void SeedReferenceData()
    {
        // Seed TimeZones
        var timeZones = new List<AppTimeZone>
        {
            new() { TimeZoneId = 1, TimeZoneName = "Eastern", UtcOffSet = "-05:00" },
            new() { TimeZoneId = 2, TimeZoneName = "Central", UtcOffSet = "-06:00" },
            new() { TimeZoneId = 3, TimeZoneName = "Pacific", UtcOffSet = "-08:00" }
        };
        _context.TimeZones.AddRange(timeZones);

        // Seed Cities
        var cities = new List<City>
        {
            new() { CityId = 1, CityName = "New York", TimeZoneId = 1 },
            new() { CityId = 2, CityName = "Chicago", TimeZoneId = 2 },
            new() { CityId = 3, CityName = "Los Angeles", TimeZoneId = 3 }
        };
        _context.Cities.AddRange(cities);

        // Seed EventCodes
        var eventCodes = new List<EventCode>
        {
            new() { EventCodeId = 1, Code = "W", Description = "Released", LongDescription = "Equipment released" },
            new() { EventCodeId = 2, Code = "Z", Description = "Placed", LongDescription = "Equipment placed" },
            new() { EventCodeId = 3, Code = "A", Description = "Arrival", LongDescription = "Arrived at location" },
            new() { EventCodeId = 4, Code = "D", Description = "Departure", LongDescription = "Departed location" }
        };
        _context.EventCodes.AddRange(eventCodes);

        _context.SaveChanges();
    }

    private static Stream CreateCsvStream(string csvContent)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
    }

    #region CSV Parsing Tests

    [Test]
    public async Task ProcessEventsFileAsync_ValidCsv_ParsesCorrectly()
    {
        // Arrange
        var csv = @"Equipment Id,Event Code,City Id,Event Time
EQ001,W,1,2024-01-01 08:00:00
EQ001,Z,2,2024-01-01 16:00:00";

        using var stream = CreateCsvStream(csv);

        // Act
        var result = await _service.ProcessEventsFileAsync(stream);

        // Assert
        Assert.That(result.EventsProcessed, Is.EqualTo(2));
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public async Task ProcessEventsFileAsync_EmptyCsv_ReturnsZeroEvents()
    {
        // Arrange
        var csv = @"Equipment Id,Event Code,City Id,Event Time";
        using var stream = CreateCsvStream(csv);

        // Act
        var result = await _service.ProcessEventsFileAsync(stream);

        // Assert
        Assert.That(result.EventsProcessed, Is.EqualTo(0));
        Assert.That(result.TripsCreated, Is.EqualTo(0));
    }

    [Test]
    public async Task ProcessEventsFileAsync_MultipleEquipments_ParsesAll()
    {
        // Arrange
        var csv = @"Equipment Id,Event Code,City Id,Event Time
EQ001,W,1,2024-01-01 08:00:00
EQ002,W,2,2024-01-01 09:00:00
EQ001,Z,2,2024-01-01 16:00:00
EQ002,Z,3,2024-01-01 17:00:00";

        using var stream = CreateCsvStream(csv);

        // Act
        var result = await _service.ProcessEventsFileAsync(stream);

        // Assert
        Assert.That(result.EventsProcessed, Is.EqualTo(4));
        Assert.That(result.TripsCreated, Is.EqualTo(2));
    }

    #endregion

    #region Trip Creation Logic Tests

    [Test]
    public async Task ProcessEventsFileAsync_WEventStartsTrip_ZEventEndsTrip()
    {
        // Arrange
        var csv = @"Equipment Id,Event Code,City Id,Event Time
EQ001,W,1,2024-01-01 08:00:00
EQ001,Z,2,2024-01-01 16:00:00";

        using var stream = CreateCsvStream(csv);

        // Act
        var result = await _service.ProcessEventsFileAsync(stream);

        // Assert
        Assert.That(result.TripsCreated, Is.EqualTo(1));

        var trip = await _context.Trips.FirstOrDefaultAsync();
        Assert.That(trip, Is.Not.Null);
        Assert.That(trip!.IsComplete, Is.True);
        Assert.That(trip.OriginCityId, Is.EqualTo(1));
        Assert.That(trip.DestinationCityId, Is.EqualTo(2));
    }

    [Test]
    public async Task ProcessEventsFileAsync_TripWithoutZEvent_MarkedIncomplete()
    {
        // Arrange
        var csv = @"Equipment Id,Event Code,City Id,Event Time
EQ001,W,1,2024-01-01 08:00:00
EQ001,A,2,2024-01-01 12:00:00";

        using var stream = CreateCsvStream(csv);

        // Act
        var result = await _service.ProcessEventsFileAsync(stream);

        // Assert
        Assert.That(result.TripsCreated, Is.EqualTo(1));

        var trip = await _context.Trips.FirstOrDefaultAsync();
        Assert.That(trip, Is.Not.Null);
        Assert.That(trip!.IsComplete, Is.False);
    }

    [Test]
    public async Task ProcessEventsFileAsync_ConsecutiveWEvents_CreatesWarningAndClosesIncompleteTrip()
    {
        // Arrange
        var csv = @"Equipment Id,Event Code,City Id,Event Time
EQ001,W,1,2024-01-01 08:00:00
EQ001,W,2,2024-01-01 12:00:00
EQ001,Z,3,2024-01-01 16:00:00";

        using var stream = CreateCsvStream(csv);

        // Act
        var result = await _service.ProcessEventsFileAsync(stream);

        // Assert
        Assert.That(result.TripsCreated, Is.EqualTo(2));
        Assert.That(result.Warnings, Has.Some.Contains("Incomplete trip"));

        var trips = await _context.Trips.ToListAsync();
        Assert.That(trips.Count(t => t.IsComplete), Is.EqualTo(1));
        Assert.That(trips.Count(t => !t.IsComplete), Is.EqualTo(1));
    }

    [Test]
    public async Task ProcessEventsFileAsync_OrphanedZEvent_CreatesWarning()
    {
        // Arrange
        var csv = @"Equipment Id,Event Code,City Id,Event Time
EQ001,Z,1,2024-01-01 08:00:00";

        using var stream = CreateCsvStream(csv);

        // Act
        var result = await _service.ProcessEventsFileAsync(stream);

        // Assert
        Assert.That(result.TripsCreated, Is.EqualTo(0));
        Assert.That(result.Warnings, Has.Some.Contains("Orphaned Z event"));
    }

    [Test]
    public async Task ProcessEventsFileAsync_CalculatesTotalTripTime_Correctly()
    {
        // Arrange - 8 hours trip (accounting for timezone conversion)
        var csv = @"Equipment Id,Event Code,City Id,Event Time
EQ001,W,1,2024-01-01 08:00:00
EQ001,Z,1,2024-01-01 16:00:00";

        using var stream = CreateCsvStream(csv);

        // Act
        var result = await _service.ProcessEventsFileAsync(stream);

        // Assert
        var trip = await _context.Trips.FirstOrDefaultAsync();
        Assert.That(trip, Is.Not.Null);
        Assert.That(trip!.TotalTime, Is.Not.Null);
        Assert.That(trip.TotalTime!.Value.TotalHours, Is.EqualTo(8).Within(0.01));
    }

    #endregion

    #region Event Ordering Tests

    [Test]
    public async Task ProcessEventsFileAsync_EventsOrderedByUtcTime()
    {
        // Arrange - Events out of order in CSV
        var csv = @"Equipment Id,Event Code,City Id,Event Time
EQ001,Z,2,2024-01-01 16:00:00
EQ001,W,1,2024-01-01 08:00:00";

        using var stream = CreateCsvStream(csv);

        // Act
        var result = await _service.ProcessEventsFileAsync(stream);

        // Assert - Should still create complete trip because events are reordered
        Assert.That(result.TripsCreated, Is.EqualTo(1));

        var trip = await _context.Trips.FirstOrDefaultAsync();
        Assert.That(trip, Is.Not.Null);
        Assert.That(trip!.IsComplete, Is.True);
    }

    [Test]
    public async Task ProcessEventsFileAsync_MultipleEquipments_EventsGroupedCorrectly()
    {
        // Arrange - Interleaved events for different equipment
        var csv = @"Equipment Id,Event Code,City Id,Event Time
EQ001,W,1,2024-01-01 08:00:00
EQ002,W,2,2024-01-01 09:00:00
EQ001,A,2,2024-01-01 12:00:00
EQ002,Z,3,2024-01-01 14:00:00
EQ001,Z,3,2024-01-01 16:00:00";

        using var stream = CreateCsvStream(csv);

        // Act
        var result = await _service.ProcessEventsFileAsync(stream);

        // Assert
        Assert.That(result.TripsCreated, Is.EqualTo(2));

        var trips = await _context.Trips.ToListAsync();
        Assert.That(trips.All(t => t.IsComplete), Is.True);
    }

    #endregion

    #region Data Persistence Tests

    [Test]
    public async Task ProcessEventsFileAsync_SavesTripsToDatabase()
    {
        // Arrange
        var csv = @"Equipment Id,Event Code,City Id,Event Time
EQ001,W,1,2024-01-01 08:00:00
EQ001,Z,2,2024-01-01 16:00:00";

        using var stream = CreateCsvStream(csv);

        // Act
        await _service.ProcessEventsFileAsync(stream);

        // Assert
        var tripCount = await _context.Trips.CountAsync();
        Assert.That(tripCount, Is.EqualTo(1));
    }

    [Test]
    public async Task ProcessEventsFileAsync_SavesEventsToDatabase()
    {
        // Arrange
        var csv = @"Equipment Id,Event Code,City Id,Event Time
EQ001,W,1,2024-01-01 08:00:00
EQ001,A,2,2024-01-01 12:00:00
EQ001,Z,3,2024-01-01 16:00:00";

        using var stream = CreateCsvStream(csv);

        // Act
        await _service.ProcessEventsFileAsync(stream);

        // Assert
        var eventCount = await _context.Events.CountAsync();
        Assert.That(eventCount, Is.EqualTo(3));
    }

    [Test]
    public async Task ProcessEventsFileAsync_EventsLinkedToTrips()
    {
        // Arrange
        var csv = @"Equipment Id,Event Code,City Id,Event Time
EQ001,W,1,2024-01-01 08:00:00
EQ001,A,2,2024-01-01 12:00:00
EQ001,Z,3,2024-01-01 16:00:00";

        using var stream = CreateCsvStream(csv);

        // Act
        await _service.ProcessEventsFileAsync(stream);

        // Assert
        var trip = await _context.Trips.Include(t => t.Events).FirstOrDefaultAsync();
        Assert.That(trip, Is.Not.Null);
        Assert.That(trip!.Events.Count, Is.EqualTo(3));
    }

    #endregion

    #region Time Zone Conversion Tests

    [Test]
    public async Task ProcessEventsFileAsync_ConvertsEasternToUtc()
    {
        // Arrange - Eastern time is UTC-5
        var csv = @"Equipment Id,Event Code,City Id,Event Time
EQ001,W,1,2024-01-01 08:00:00
EQ001,Z,1,2024-01-01 16:00:00";

        using var stream = CreateCsvStream(csv);

        // Act
        await _service.ProcessEventsFileAsync(stream);

        // Assert - Local 08:00 EST should be 13:00 UTC
        var evt = await _context.Events.OrderBy(e => e.EventTimeUtc).FirstOrDefaultAsync();
        Assert.That(evt, Is.Not.Null);
        Assert.That(evt!.EventTimeUtc.Hour, Is.EqualTo(13));
    }

    [Test]
    public async Task ProcessEventsFileAsync_ConvertsCentralToUtc()
    {
        // Arrange - Central time is UTC-6
        var csv = @"Equipment Id,Event Code,City Id,Event Time
EQ001,W,2,2024-01-01 08:00:00
EQ001,Z,2,2024-01-01 16:00:00";

        using var stream = CreateCsvStream(csv);

        // Act
        await _service.ProcessEventsFileAsync(stream);

        // Assert - Local 08:00 CST should be 14:00 UTC
        var evt = await _context.Events.OrderBy(e => e.EventTimeUtc).FirstOrDefaultAsync();
        Assert.That(evt, Is.Not.Null);
        Assert.That(evt!.EventTimeUtc.Hour, Is.EqualTo(14));
    }

    [Test]
    public async Task ProcessEventsFileAsync_ConvertsPacificToUtc()
    {
        // Arrange - Pacific time is UTC-8
        var csv = @"Equipment Id,Event Code,City Id,Event Time
EQ001,W,3,2024-01-01 08:00:00
EQ001,Z,3,2024-01-01 16:00:00";

        using var stream = CreateCsvStream(csv);

        // Act
        await _service.ProcessEventsFileAsync(stream);

        // Assert - Local 08:00 PST should be 16:00 UTC
        var evt = await _context.Events.OrderBy(e => e.EventTimeUtc).FirstOrDefaultAsync();
        Assert.That(evt, Is.Not.Null);
        Assert.That(evt!.EventTimeUtc.Hour, Is.EqualTo(16));
    }

    [Test]
    public async Task ProcessEventsFileAsync_PreservesLocalTime()
    {
        // Arrange
        var csv = @"Equipment Id,Event Code,City Id,Event Time
EQ001,W,1,2024-01-01 08:30:00
EQ001,Z,1,2024-01-01 16:30:00";

        using var stream = CreateCsvStream(csv);

        // Act
        await _service.ProcessEventsFileAsync(stream);

        // Assert - Get the W event (first by UTC time)
        var evt = await _context.Events.OrderBy(e => e.EventTimeUtc).FirstOrDefaultAsync();
        Assert.That(evt, Is.Not.Null);
        Assert.That(evt!.EventTimeLocal.Hour, Is.EqualTo(8));
        Assert.That(evt.EventTimeLocal.Minute, Is.EqualTo(30));
    }

    #endregion

    #region Validation Tests

    [Test]
    public async Task ProcessEventsFileAsync_UnknownCity_CreatesWarning()
    {
        // Arrange
        var csv = @"Equipment Id,Event Code,City Id,Event Time
EQ001,W,999,2024-01-01 08:00:00";

        using var stream = CreateCsvStream(csv);

        // Act
        var result = await _service.ProcessEventsFileAsync(stream);

        // Assert
        Assert.That(result.Warnings, Has.Some.Contains("Unknown city_id: 999"));
        Assert.That(result.EventsProcessed, Is.EqualTo(0));
    }

    [Test]
    public async Task ProcessEventsFileAsync_InvalidEventCode_CreatesWarning()
    {
        // Arrange
        var csv = @"Equipment Id,Event Code,City Id,Event Time
EQ001,X,1,2024-01-01 08:00:00";

        using var stream = CreateCsvStream(csv);

        // Act
        var result = await _service.ProcessEventsFileAsync(stream);

        // Assert
        Assert.That(result.Warnings, Has.Some.Contains("Unknown event_code: X"));
        Assert.That(result.EventsProcessed, Is.EqualTo(0));
    }

    [Test]
    public async Task ProcessEventsFileAsync_MixedValidAndInvalid_ProcessesValidOnly()
    {
        // Arrange
        var csv = @"Equipment Id,Event Code,City Id,Event Time
EQ001,W,1,2024-01-01 08:00:00
EQ001,X,1,2024-01-01 10:00:00
EQ001,Z,999,2024-01-01 12:00:00
EQ001,Z,2,2024-01-01 16:00:00";

        using var stream = CreateCsvStream(csv);

        // Act
        var result = await _service.ProcessEventsFileAsync(stream);

        // Assert
        Assert.That(result.EventsProcessed, Is.EqualTo(2)); // Only W and valid Z
        Assert.That(result.Warnings.Count, Is.EqualTo(2)); // Invalid event code and unknown city
    }

    [Test]
    public async Task ProcessEventsFileAsync_AllValidEventCodes_Accepted()
    {
        // Arrange - Test all valid codes: W, Z, A, D
        var csv = @"Equipment Id,Event Code,City Id,Event Time
EQ001,W,1,2024-01-01 08:00:00
EQ001,A,2,2024-01-01 10:00:00
EQ001,D,2,2024-01-01 11:00:00
EQ001,Z,3,2024-01-01 16:00:00";

        using var stream = CreateCsvStream(csv);

        // Act
        var result = await _service.ProcessEventsFileAsync(stream);

        // Assert
        Assert.That(result.EventsProcessed, Is.EqualTo(4));
        Assert.That(result.Warnings.Where(w => w.Contains("Unknown event_code")).Count(), Is.EqualTo(0));
    }

    #endregion

    #region Integration Tests

    [Test]
    public async Task ProcessEventsFileAsync_CompleteScenario_WorksCorrectly()
    {
        // Arrange - Complete realistic scenario
        var csv = @"Equipment Id,Event Code,City Id,Event Time
RAIL001,W,1,2024-01-01 06:00:00
RAIL001,D,1,2024-01-01 06:30:00
RAIL001,A,2,2024-01-01 14:00:00
RAIL001,D,2,2024-01-01 15:00:00
RAIL001,A,3,2024-01-01 22:00:00
RAIL001,Z,3,2024-01-01 22:30:00
RAIL002,W,2,2024-01-01 08:00:00
RAIL002,Z,1,2024-01-01 18:00:00";

        using var stream = CreateCsvStream(csv);

        // Act
        var result = await _service.ProcessEventsFileAsync(stream);

        // Assert
        Assert.That(result.Errors, Is.Empty);
        Assert.That(result.EventsProcessed, Is.EqualTo(8));
        Assert.That(result.TripsCreated, Is.EqualTo(2));

        var trips = await _context.Trips.Include(t => t.Events).ToListAsync();
        Assert.That(trips.All(t => t.IsComplete), Is.True);

        var rail001Trip = trips.First(t => t.EquipmentId == "RAIL001");
        Assert.That(rail001Trip.Events.Count, Is.EqualTo(6));
        Assert.That(rail001Trip.OriginCityId, Is.EqualTo(1));
        Assert.That(rail001Trip.DestinationCityId, Is.EqualTo(3));
    }

    #endregion
}
