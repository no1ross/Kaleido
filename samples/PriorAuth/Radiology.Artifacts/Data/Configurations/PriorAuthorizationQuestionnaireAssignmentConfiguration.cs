using Kaleido.Samples.PriorAuth.Radiology.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.Radiology.Data.Configurations;

internal sealed class PriorAuthorizationQuestionnaireAssignmentConfiguration : IEntityTypeConfiguration<PriorAuthorizationQuestionnaireAssignment>
{
    public void Configure(EntityTypeBuilder<PriorAuthorizationQuestionnaireAssignment> builder)
    {
        builder.HasKey(x => x.PriorAuthorizationQuestionnaireAssignmentId);

        builder.Property(x => x.StepName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.QuestionnaireId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.QuestionnaireVersion)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => new { x.ProcessId, x.StepName })
            .IsUnique();

        builder.HasIndex(x => x.QuestionnaireId);
    }
}
