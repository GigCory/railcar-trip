using Microsoft.EntityFrameworkCore;
using RailcarTrips.Server.Data.Entities;

namespace RailcarTrips.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AppTimeZone> TimeZones { get; set; }
    public DbSet<City> Cities { get; set; }
    public DbSet<EventCode> EventCodes { get; set; }
    public DbSet<Trip> Trips { get; set; }
    public DbSet<Event> Events { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure TimeZone entity
        modelBuilder.Entity<AppTimeZone>(entity =>
        {
            entity.HasIndex(e => e.TimeZoneName).IsUnique();
        });

        // Configure City entity
        modelBuilder.Entity<City>(entity =>
        {
            entity.HasOne(c => c.TimeZone)
                .WithMany(tz => tz.Cities)
                .HasForeignKey(c => c.TimeZoneId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure EventCode entity
        modelBuilder.Entity<EventCode>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();
        });

        // Configure Trip entity
        modelBuilder.Entity<Trip>(entity =>
        {
            entity.HasOne(t => t.OriginCity)
                .WithMany()
                .HasForeignKey(t => t.OriginCityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.DestinationCity)
                .WithMany()
                .HasForeignKey(t => t.DestinationCityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(t => t.EquipmentId);
        });

        // Configure Event entity
        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasOne(e => e.Trip)
                .WithMany(t => t.Events)
                .HasForeignKey(e => e.TripId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.City)
                .WithMany(c => c.Events)
                .HasForeignKey(e => e.CityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.EventCode)
                .WithMany(ec => ec.Events)
                .HasForeignKey(e => e.EventCodeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.EquipmentId);
            entity.HasIndex(e => e.EventTimeUtc);
        });
    }
}
