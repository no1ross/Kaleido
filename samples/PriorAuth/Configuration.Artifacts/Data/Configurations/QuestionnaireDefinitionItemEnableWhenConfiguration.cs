using Kaleido.Samples.PriorAuth.Configuration.Artifacts.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.Configuration.Artifacts.Data.Configurations;

internal sealed class QuestionnaireDefinitionItemEnableWhenConfiguration : IEntityTypeConfiguration<QuestionnaireDefinitionItemEnableWhen>
{
    public void Configure(EntityTypeBuilder<QuestionnaireDefinitionItemEnableWhen> builder)
    {
        builder.HasKey(x => x.QuestionnaireDefinitionItemEnableWhenId);

        builder.Property(x => x.QuestionBindingKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Operator)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.AnswerValue)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => new { x.QuestionnaireDefinitionItemId, x.QuestionBindingKey, x.Operator, x.AnswerValue })
            .IsUnique();
    }
}
