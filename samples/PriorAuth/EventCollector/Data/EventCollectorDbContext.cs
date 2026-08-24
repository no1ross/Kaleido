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
            entity.Property(x => x.ProcessId);
            entity.Property(x => x.OccurredOn);
            entity.Property(x => x.PayloadJson);
            entity.HasIndex(x => x.ProcessId);
            entity.HasIndex(x => x.OccurredOn);
        });
    }
}

public sealed class CollectedEvent
{
    public long Id { get; set; }
    public Guid? ProcessId { get; set; }
    public DateTimeOffset OccurredOn { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTimeOffset ReceivedOn { get; set; }
}
