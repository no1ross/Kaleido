using Kaleido.Provider.SQLite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Provider.SQLite.Configuration;

[ExcludeFromCodeCoverage]
internal sealed class ProcessorContextEntityConfiguration
    : IEntityTypeConfiguration<ProcessorContextEntity>
{
    internal const string TableName = "ProcessContexts";

    public void Configure(
        EntityTypeBuilder<ProcessorContextEntity> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(x => x.ProcessId);
        builder.Property(x => x.LatestRequestId).HasMaxLength(100);

        // The UPDATE is conditional on the version the row was read with, so a concurrent save by
        // another instance fails instead of overwriting.
        builder.Property(x => x.Version).IsConcurrencyToken();

        builder.Property(x => x.State).HasConversion<string>().IsRequired();
        builder.Property(x => x.TargetProcessorName).HasMaxLength(200);
        builder.Property(x => x.CreatedUtc).IsRequired();
        builder.Property(x => x.UpdatedUtc).IsRequired();

        builder.HasMany(x => x.Steps)
            .WithOne(x => x.Context)
            .HasForeignKey(x => x.ProcessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.AvailableSteps)
            .WithOne(x => x.Context)
            .HasForeignKey(x => x.ProcessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RequiredStep)
            .WithOne(x => x.Context)
            .HasForeignKey<ProcessorRequiredStepEntity>(x => x.ProcessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.State);
        builder.HasIndex(x => x.CreatedUtc);
        builder.HasIndex(x => x.UpdatedUtc);
    }
}
