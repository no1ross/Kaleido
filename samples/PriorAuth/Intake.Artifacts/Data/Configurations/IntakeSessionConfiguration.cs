using Kaleido.Samples.PriorAuth.Intake.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.Intake.Data.Configurations;

internal sealed class IntakeSessionConfiguration : IEntityTypeConfiguration<IntakeSession>
{
    public void Configure(EntityTypeBuilder<IntakeSession> builder)
    {
        builder.HasKey(x => x.IntakeSessionId);

        builder.HasIndex(x => x.ProcessId)
            .IsUnique();

        builder.HasIndex(x => x.CreatedUtc);

        builder.HasOne(x => x.Member)
            .WithOne(x => x.IntakeSession)
            .HasForeignKey<IntakeSessionMember>(x => x.IntakeSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Procedure)
            .WithOne(x => x.IntakeSession)
            .HasForeignKey<IntakeSessionProcedure>(x => x.IntakeSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
