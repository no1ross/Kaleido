using Kaleido.Samples.PriorAuth.Configuration.Artifacts.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.Configuration.Artifacts.Data.Configurations;

internal sealed class QuestionnaireDefinitionItemConfiguration : IEntityTypeConfiguration<QuestionnaireDefinitionItem>
{
    public void Configure(EntityTypeBuilder<QuestionnaireDefinitionItem> builder)
    {
        builder.HasKey(x => x.QuestionnaireDefinitionItemId);

        builder.Property(x => x.LinkId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Text)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Type)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.BindingKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.DefaultValue)
            .HasMaxLength(100);

        builder.HasIndex(x => new { x.QuestionnaireDefinitionId, x.LinkId })
            .IsUnique();

        builder.HasIndex(x => new { x.QuestionnaireDefinitionId, x.Order });

        builder.HasMany(x => x.AnswerOptions)
            .WithOne(x => x.QuestionnaireDefinitionItem)
            .HasForeignKey(x => x.QuestionnaireDefinitionItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.EnableWhen)
            .WithOne(x => x.QuestionnaireDefinitionItem)
            .HasForeignKey(x => x.QuestionnaireDefinitionItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
