using Kaleido.Samples.PriorAuth.Configuration.Artifacts.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.Configuration.Artifacts.Data.Configurations;

internal sealed class ProcedureModalityRuleConfiguration : IEntityTypeConfiguration<ProcedureModalityRule>
{
    public void Configure(EntityTypeBuilder<ProcedureModalityRule> builder)
    {
        builder.HasKey(x => x.ProcedureModalityRuleId);

        builder.Property(x => x.CodeSystem)
            .HasConversion<string>();

        builder.Property(x => x.Modality)
            .HasConversion<string>();

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => new { x.CodeSystem, x.CodeRangeStart, x.CodeRangeEnd });
    }
}
