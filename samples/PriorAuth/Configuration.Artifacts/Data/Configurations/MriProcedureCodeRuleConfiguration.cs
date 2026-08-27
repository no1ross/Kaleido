using Kaleido.Samples.PriorAuth.Configuration.Artifacts.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.Configuration.Artifacts.Data.Configurations;

internal sealed class MriProcedureCodeRuleConfiguration : IEntityTypeConfiguration<MriProcedureCodeRule>
{
    public void Configure(EntityTypeBuilder<MriProcedureCodeRule> builder)
    {
        builder.HasKey(x => x.MriProcedureCodeRuleId);

        builder.Property(x => x.SelectedCodeSystem)
            .HasConversion<string>();

        builder.Property(x => x.SelectedCodeValue)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.BodyPart)
            .HasConversion<string>();

        builder.Property(x => x.Laterality)
            .HasConversion<string>();

        builder.Property(x => x.Contrast)
            .HasConversion<string>();

        builder.Property(x => x.ResolvedCodeSystem)
            .HasConversion<string>();

        builder.Property(x => x.ResolvedCodeValue)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => new { x.SelectedCodeSystem, x.SelectedCodeValue, x.BodyPart, x.Laterality, x.Contrast })
            .IsUnique();
    }
}
