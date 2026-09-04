using Kaleido.Samples.PriorAuth.Intake.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.Intake.Data.Configurations;

internal sealed class IntakeSessionProcedureConfiguration : IEntityTypeConfiguration<IntakeSessionProcedure>
{
    public void Configure(EntityTypeBuilder<IntakeSessionProcedure> builder)
    {
        builder.HasKey(x => x.IntakeSessionId);

        builder.Property(x => x.CodeValue)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.CodeSystem)
            .HasConversion<string>();

        builder.Property(x => x.ResolvedProcessorName)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => x.ResolvedProcessorName);
    }
}
