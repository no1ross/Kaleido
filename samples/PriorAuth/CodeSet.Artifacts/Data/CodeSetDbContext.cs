using Kaleido.Samples.PriorAuth.CodeSet.Artifacts.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.CodeSet.Artifacts.Data;

public sealed class CodeSetDbContext(
    DbContextOptions<CodeSetDbContext> options) : DbContext(options)
{
    public DbSet<ProcedureCode> ProcedureCodes => Set<ProcedureCode>();

    public DbSet<DiagnosisCode> DiagnosisCodes => Set<DiagnosisCode>();

    public DbSet<MedicalSpecialty> MedicalSpecialties => Set<MedicalSpecialty>();

    public DbSet<CodeGrouper> CodeGroupers => Set<CodeGrouper>();

    public DbSet<ProcedureCodeSpecialtyAssignment> ProcedureCodeSpecialtyAssignments => Set<ProcedureCodeSpecialtyAssignment>();

    public DbSet<ProcedureCodeGrouperAssignment> ProcedureCodeGrouperAssignments => Set<ProcedureCodeGrouperAssignment>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(CodeSetDbContext).Assembly);
    }
}
