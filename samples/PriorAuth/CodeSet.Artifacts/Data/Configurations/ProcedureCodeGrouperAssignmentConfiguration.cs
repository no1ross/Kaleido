using Kaleido.Samples.PriorAuth.CodeSet.Artifacts.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.CodeSet.Artifacts.Data.Configurations;

internal sealed class ProcedureCodeGrouperAssignmentConfiguration : IEntityTypeConfiguration<ProcedureCodeGrouperAssignment>
{
    public void Configure(EntityTypeBuilder<ProcedureCodeGrouperAssignment> builder)
    {
        builder.HasKey(x => new { x.ProcedureCodeId, x.CodeGrouperId });

        builder.HasIndex(x => x.CodeGrouperId);

        builder.HasOne(x => x.ProcedureCode)
            .WithMany(x => x.GrouperAssignments)
            .HasForeignKey(x => x.ProcedureCodeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CodeGrouper)
            .WithMany(x => x.ProcedureCodeAssignments)
            .HasForeignKey(x => x.CodeGrouperId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
