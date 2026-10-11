using Kaleido.Samples.PriorAuth.Configuration.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.Configuration.Data.Configurations;

internal sealed class ClinicalPathwayTransitionConfiguration : IEntityTypeConfiguration<ClinicalPathwayTransition>
{
    public void Configure(EntityTypeBuilder<ClinicalPathwayTransition> builder)
    {
        builder.HasKey(x => x.ClinicalPathwayTransitionId);

        builder.Property(x => x.FromQuestionnaireId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.WhenBindingKey).HasMaxLength(100);
        builder.Property(x => x.Operator).HasMaxLength(20).IsRequired();
        builder.Property(x => x.AnswerValue).HasMaxLength(200);
        builder.Property(x => x.ToQuestionnaireId).HasMaxLength(100);
        builder.Property(x => x.Determination).HasMaxLength(100);

        builder.HasIndex(x => x.FromQuestionnaireId);
    }
}