using Kaleido.Samples.PriorAuth.Intake.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Intake.Data;

public sealed class IntakeDbContext(
    DbContextOptions<IntakeDbContext> options) : DbContext(options)
{
    public DbSet<IntakeSession> IntakeSessions => Set<IntakeSession>();

    public DbSet<IntakeSessionMember> IntakeSessionMembers => Set<IntakeSessionMember>();

    public DbSet<IntakeSessionProcedure> IntakeSessionProcedures => Set<IntakeSessionProcedure>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(IntakeDbContext).Assembly);
    }
}
