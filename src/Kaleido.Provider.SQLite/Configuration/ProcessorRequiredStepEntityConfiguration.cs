using Kaleido.Provider.SQLite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Provider.SQLite.Configuration;

[ExcludeFromCodeCoverage]
internal sealed class ProcessorRequiredStepEntityConfiguration
    : IEntityTypeConfiguration<ProcessorRequiredStepEntity>
{
    public void Configure(
        EntityTypeBuilder<ProcessorRequiredStepEntity> builder)
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
