using Kaleido.Samples.PriorAuth.Configuration.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.Configuration.Data.Configurations;

internal sealed class QuestionnaireMappingRuleConfiguration : IEntityTypeConfiguration<QuestionnaireMappingRule>
{
    public void Configure(EntityTypeBuilder<QuestionnaireMappingRule> builder)
    {
        builder.HasKey(x => x.QuestionnaireMappingRuleId);

        builder.Property(x => x.StepName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.PlanId)
            .HasMaxLength(50);

        builder.Property(x => x.LineOfBusiness)
            .HasMaxLength(50);

        builder.Property(x => x.ProcedureModality)
            .HasConversion<string>();

        builder.Property(x => x.ProcedureCodeValue)
            .HasMaxLength(50);

        builder.HasIndex(x => x.StepName);

        builder.HasIndex(x => new { x.StepName, x.PlanId, x.LineOfBusiness, x.ProcedureModality, x.ProcedureCodeValue, x.Priority });
    }
}
