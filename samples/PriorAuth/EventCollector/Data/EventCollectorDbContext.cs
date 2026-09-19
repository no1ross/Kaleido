using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.EventCollector.Data;

public sealed class EventCollectorDbContext(
    DbContextOptions<EventCollectorDbContext> options)
    : DbContext(options)
{
    public DbSet<CollectedEvent> Events =>
        Set<CollectedEvent>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CollectedEvent>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(256);
            entity.Property(x => x.RequestId).HasMaxLength(128);
            entity.Property(x => x.ServiceName).HasMaxLength(256);
            entity.Property(x => x.ProcessId);
            entity.Property(x => x.StepName).HasMaxLength(256);
            entity.Property(x => x.OccurredOn);
            entity.Property(x => x.ReceivedOn);
            entity.Property(x => x.ContextJson);
            entity.Property(x => x.EventJson);
            entity.HasIndex(x => x.RequestId);
            entity.HasIndex(x => x.ProcessId);
            entity.HasIndex(x => x.OccurredOn);
        });
    }
}

public sealed class CollectedEvent
{
    public long Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public Guid? ProcessId { get; set; }
    public string? StepName { get; set; }
    public DateTime OccurredOn { get; set; }
    public DateTime ReceivedOn { get; set; }
    public string ContextJson { get; set; } = string.Empty;
    public string EventJson { get; set; } = string.Empty;
}
