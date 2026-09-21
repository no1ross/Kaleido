using Kaleido.Samples.PriorAuth.CodeSet.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.CodeSet.Data.Configurations;

internal sealed class ProcedureCodeSpecialtyAssignmentConfiguration : IEntityTypeConfiguration<ProcedureCodeSpecialtyAssignment>
{
    public void Configure(EntityTypeBuilder<ProcedureCodeSpecialtyAssignment> builder)
    {
        builder.HasKey(x => new { x.ProcedureCodeId, x.MedicalSpecialtyId });

        builder.HasIndex(x => x.MedicalSpecialtyId);

        builder.HasOne(x => x.ProcedureCode)
            .WithMany(x => x.SpecialtyAssignments)
            .HasForeignKey(x => x.ProcedureCodeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.MedicalSpecialty)
            .WithMany(x => x.ProcedureCodeAssignments)
            .HasForeignKey(x => x.MedicalSpecialtyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
