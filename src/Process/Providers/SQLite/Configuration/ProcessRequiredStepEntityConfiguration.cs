using Kaleido.Process.Providers.SQLite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Process.Providers.SQLite.Configuration;

internal sealed class ProcessRequiredStepEntityConfiguration
    : IEntityTypeConfiguration<ProcessRequiredStepEntity>
{
    public void Configure(
        EntityTypeBuilder<ProcessRequiredStepEntity> builder)
    {
        builder.ToTable(
            "ProcessRequiredSteps");

        builder.HasKey(
            x => x.ProcessId);

        builder.Property(
                x => x.ProcessorName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(
                x => x.StepName)
            .HasMaxLength(200)
            .IsRequired();
    }
}
