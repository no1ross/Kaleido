using Kaleido.Provider.SQLite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Provider.SQLite.Configuration;

[ExcludeFromCodeCoverage]
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
