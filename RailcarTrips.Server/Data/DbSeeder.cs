using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using RailcarTrips.Server.Data.Entities;
using System.Globalization;

namespace RailcarTrips.Server.Data;

public class DbSeeder
{
    private readonly AppDbContext _context;
    private readonly ILogger<DbSeeder> _logger;
    private readonly IWebHostEnvironment _environment;

    public DbSeeder(AppDbContext context, ILogger<DbSeeder> logger, IWebHostEnvironment environment)
    {
        _context = context;
        _logger = logger;
        _environment = environment;
    }

    public async Task SeedAsync()
    {
        await _context.Database.MigrateAsync();

        if (!await _context.TimeZones.AnyAsync())
        {
            await SeedTimeZonesAndCitiesAsync();
        }

        if (!await _context.EventCodes.AnyAsync())
        {
            await SeedEventCodesAsync();
        }
    }

    private async Task SeedTimeZonesAndCitiesAsync()
    {
        var seedPath = Path.Combine(_environment.ContentRootPath, "Data", "SeedData", "canadian_cities.csv");

        if (!File.Exists(seedPath))
        {
            _logger.LogWarning("Seed file not found: {Path}", seedPath);
            return;
        }

        _logger.LogInformation("Seeding time zones and cities from {Path}", seedPath);

        using var reader = new StreamReader(seedPath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim
        });

        var records = csv.GetRecords<CityRecord>().ToList();

        // Extract unique time zones and create them
        var timeZoneMap = new Dictionary<string, AppTimeZone>();
        foreach (var record in records)
        {
            if (!timeZoneMap.ContainsKey(record.TimeZone))
            {
                var utcOffset = GetUtcOffset(record.TimeZone);
                var tz = new AppTimeZone
                {
                    TimeZoneName = record.TimeZone,
                    UtcOffSet = utcOffset,
                    UpdatedAt = DateTime.UtcNow
                };
                timeZoneMap[record.TimeZone] = tz;
                _context.TimeZones.Add(tz);
            }
        }

        await _context.SaveChangesAsync();

        // Reload time zones to get their IDs
        var savedTimeZones = await _context.TimeZones.ToDictionaryAsync(tz => tz.TimeZoneName);

        // Create cities
        foreach (var record in records)
        {
            var city = new City
            {
                CityId = record.CityId,
                CityName = record.CityName,
                TimeZoneId = savedTimeZones[record.TimeZone].TimeZoneId
            };
            _context.Cities.Add(city);
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded {TzCount} time zones and {CityCount} cities", timeZoneMap.Count, records.Count);
    }

    private async Task SeedEventCodesAsync()
    {
        var seedPath = Path.Combine(_environment.ContentRootPath, "Data", "SeedData", "event_code_definitions.csv");

        if (!File.Exists(seedPath))
        {
            _logger.LogWarning("Seed file not found: {Path}", seedPath);
            return;
        }

        _logger.LogInformation("Seeding event codes from {Path}", seedPath);

        using var reader = new StreamReader(seedPath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim
        });

        var records = csv.GetRecords<EventCodeRecord>().ToList();

        foreach (var record in records)
        {
            var eventCode = new EventCode
            {
                Code = record.EventCode,
                Description = record.EventDescription,
                LongDescription = record.LongDescription
            };
            _context.EventCodes.Add(eventCode);
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded {Count} event codes", records.Count);
    }

    private static string GetUtcOffset(string timeZoneName)
    {
        return timeZoneName switch
        {
            "Pacific Standard Time" => "-08:00",
            "Mountain Standard Time" => "-07:00",
            "Canada Central Standard Time" => "-06:00",
            "Central Standard Time" => "-06:00",
            "Eastern Standard Time" => "-05:00",
            "Atlantic Standard Time" => "-04:00",
            "Newfoundland Standard Time" => "-03:30",
            _ => "+00:00"
        };
    }

    private class CityRecord
    {
        [CsvHelper.Configuration.Attributes.Name("City Id")]
        public int CityId { get; set; }

        [CsvHelper.Configuration.Attributes.Name("City Name")]
        public string CityName { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("Time Zone")]
        public string TimeZone { get; set; } = string.Empty;
    }

    private class EventCodeRecord
    {
        [CsvHelper.Configuration.Attributes.Name("Event Code")]
        public string EventCode { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("Event Description")]
        public string EventDescription { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("Long Description")]
        public string LongDescription { get; set; } = string.Empty;
    }
}
