using Microsoft.EntityFrameworkCore;
using RoutingService.Domain;
using RoutingService.Infrastructure.Outbox;

namespace RoutingService.Infrastructure.Persistence;

public sealed class RoutingDbContext : DbContext
{
    public RoutingDbContext(DbContextOptions<RoutingDbContext> options)
        : base(options)
    {
    }

    public DbSet<LogisticsNode> LogisticsNodes => Set<LogisticsNode>();
    public DbSet<LogisticsLane> LogisticsLanes => Set<LogisticsLane>();
    public DbSet<LaneSchedule> LaneSchedules => Set<LaneSchedule>();
    public DbSet<PostalCoverage> PostalCoverages => Set<PostalCoverage>();
    public DbSet<NetworkVersion> NetworkVersions => Set<NetworkVersion>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LogisticsNode>(entity =>
        {
            entity.ToTable("logistics_nodes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(40).IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(x => x.Region).HasColumnName("region").HasMaxLength(100).IsRequired();
            entity.Property(x => x.TimeZoneId).HasColumnName("time_zone_id").HasMaxLength(100).IsRequired();
            entity.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.HandlingMinutes).HasColumnName("handling_minutes").IsRequired();
            entity.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => new { x.Region, x.IsActive });
        });

        modelBuilder.Entity<LogisticsLane>(entity =>
        {
            entity.ToTable("logistics_lanes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.OriginNodeId).HasColumnName("origin_node_id").IsRequired();
            entity.Property(x => x.DestinationNodeId).HasColumnName("destination_node_id").IsRequired();
            entity.Property(x => x.CarrierCode).HasColumnName("carrier_code").HasMaxLength(80).IsRequired();
            entity.Property(x => x.ServiceLevelCode).HasColumnName("service_level_code").HasMaxLength(80).IsRequired();
            entity.Property(x => x.Mode).HasColumnName("mode").HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.TransitMinutes).HasColumnName("transit_minutes").IsRequired();
            entity.Property(x => x.MaximumWeightKg).HasColumnName("maximum_weight_kg").HasPrecision(10, 3).IsRequired();
            entity.Property(x => x.MaximumCubicWeightKg).HasColumnName("maximum_cubic_weight_kg").HasPrecision(10, 3).IsRequired();
            entity.Property(x => x.SupportsFragileItems).HasColumnName("supports_fragile_items").IsRequired();
            entity.Property(x => x.SupportsRestrictedItems).HasColumnName("supports_restricted_items").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
            entity.HasIndex(x => new { x.OriginNodeId, x.Status });
            entity.HasOne<LogisticsNode>()
                .WithMany()
                .HasForeignKey(x => x.OriginNodeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LogisticsNode>()
                .WithMany()
                .HasForeignKey(x => x.DestinationNodeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Schedules)
                .WithOne()
                .HasForeignKey(x => x.LogisticsLaneId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LaneSchedule>(entity =>
        {
            entity.ToTable("lane_schedules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.LogisticsLaneId).HasColumnName("logistics_lane_id").IsRequired();
            entity.Property(x => x.DayOfWeek).HasColumnName("day_of_week").HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.DepartureTime).HasColumnName("departure_time").IsRequired();
            entity.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        });

        modelBuilder.Entity<PostalCoverage>(entity =>
        {
            entity.ToTable("postal_coverages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.DestinationNodeId).HasColumnName("destination_node_id").IsRequired();
            entity.Property(x => x.PostalCodeFrom).HasColumnName("postal_code_from").IsRequired();
            entity.Property(x => x.PostalCodeTo).HasColumnName("postal_code_to").IsRequired();
            entity.Property(x => x.Priority).HasColumnName("priority").IsRequired();
            entity.HasIndex(x => new { x.PostalCodeFrom, x.PostalCodeTo });
            entity.HasOne<LogisticsNode>()
                .WithMany()
                .HasForeignKey(x => x.DestinationNodeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NetworkVersion>(entity =>
        {
            entity.ToTable("network_versions");
            entity.HasKey(x => x.Region);
            entity.Property(x => x.Region).HasColumnName("region").HasMaxLength(100).IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Type).HasColumnName("type").HasMaxLength(200).IsRequired();
            entity.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();
            entity.Property(x => x.ProcessedAt).HasColumnName("processed_at");
            entity.HasIndex(x => x.ProcessedAt);
        });
    }
}
