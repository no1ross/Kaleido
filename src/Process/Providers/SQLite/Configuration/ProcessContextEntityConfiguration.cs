using Kaleido.Process.Providers.SQLite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Process.Providers.SQLite.Configuration;

internal sealed class ProcessContextEntityConfiguration
    : IEntityTypeConfiguration<ProcessContextEntity>
{
    public void Configure(
        EntityTypeBuilder<ProcessContextEntity> builder)
    {
        builder.ToTable(
            "ProcessContexts");

        builder.HasKey(
            x => x.ParticipantProcessId);

        builder.Property(
                x => x.LatestRequestId)
            .HasMaxLength(100);

        builder.Property(
                x => x.State)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(
                x => x.RequiredStep)
            .HasMaxLength(200);

        builder.Property(
                x => x.CreatedUtc)
            .IsRequired();

        builder.Property(
                x => x.UpdatedUtc)
            .IsRequired();

        builder.HasMany(
                x => x.Steps)
            .WithOne(
                x => x.Context)
            .HasForeignKey(
                x => x.ParticipantProcessId)
            .OnDelete(
                DeleteBehavior.Cascade);

        builder.HasMany(
                x => x.AvailableSteps)
            .WithOne(
                x => x.Context)
            .HasForeignKey(
                x => x.ParticipantProcessId)
            .OnDelete(
                DeleteBehavior.Cascade);

        builder.HasIndex(
            x => x.State);

        builder.HasIndex(
            x => x.CreatedUtc);

        builder.HasIndex(
            x => x.UpdatedUtc);
    }
}
