using Kaleido.Samples.PriorAuth.Configuration.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.Configuration.Data.Configurations;

internal sealed class QuestionnaireDefinitionItemAnswerOptionConfiguration : IEntityTypeConfiguration<QuestionnaireDefinitionItemAnswerOption>
{
    public void Configure(EntityTypeBuilder<QuestionnaireDefinitionItemAnswerOption> builder)
    {
        builder.HasKey(x => x.QuestionnaireDefinitionItemAnswerOptionId);

        builder.Property(x => x.Value)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.DisplayText)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(x => new { x.QuestionnaireDefinitionItemId, x.Value })
            .IsUnique();

        builder.HasIndex(x => new { x.QuestionnaireDefinitionItemId, x.Order });
    }
}
