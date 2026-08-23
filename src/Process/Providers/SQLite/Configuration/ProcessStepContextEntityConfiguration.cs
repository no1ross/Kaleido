using Kaleido.Process.Providers.SQLite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Process.Providers.SQLite.Configuration;

internal sealed class ProcessStepContextEntityConfiguration
    : IEntityTypeConfiguration<ProcessStepContextEntity>
{
    public void Configure(
        EntityTypeBuilder<ProcessStepContextEntity> builder)
    {
        builder.ToTable(
            "ProcessStepContexts");

        builder.HasKey(
            x => new
            {
                x.ProcessId,
                x.StepName
            });

        builder.Property(
                x => x.StepName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(
                x => x.Version)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(
                x => x.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(
                x => x.LatestRequestId)
            .HasMaxLength(100);

        builder.HasIndex(
            x => x.Status);

        builder.HasIndex(
            x => x.LastExecuted);
    }
}