using Kaleido.Samples.PriorAuth.Configuration.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.Configuration.Data.Configurations;

internal sealed class QuestionnaireDefinitionConfiguration : IEntityTypeConfiguration<QuestionnaireDefinition>
{
    public void Configure(EntityTypeBuilder<QuestionnaireDefinition> builder)
    {
        builder.HasKey(x => x.QuestionnaireDefinitionId);

        builder.Property(x => x.QuestionnaireId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Version)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.HasIndex(x => new { x.QuestionnaireId, x.Version })
            .IsUnique();

        builder.HasIndex(x => x.Name);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.QuestionnaireDefinition)
            .HasForeignKey(x => x.QuestionnaireDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.MappingRules)
            .WithOne(x => x.QuestionnaireDefinition)
            .HasForeignKey(x => x.QuestionnaireDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
