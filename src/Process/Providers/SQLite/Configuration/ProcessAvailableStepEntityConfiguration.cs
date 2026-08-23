using Kaleido.Process.Providers.SQLite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Process.Providers.SQLite.Configuration;

internal sealed class ProcessAvailableStepEntityConfiguration
    : IEntityTypeConfiguration<ProcessAvailableStepEntity>
{
    public void Configure(
        EntityTypeBuilder<ProcessAvailableStepEntity> builder)
    {
        builder.ToTable(
            "ProcessAvailableSteps");

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
                x => x.Sequence)
            .IsRequired();

        builder.HasIndex(
            x => x.Sequence);
    }
}