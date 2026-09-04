using Kaleido.Samples.PriorAuth.Intake.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Intake.Data;

public sealed class IntakeDbContext(
    DbContextOptions<IntakeDbContext> options) : DbContext(options)
{
    public DbSet<PriorAuthorization> PriorAuthorizations => Set<PriorAuthorization>();

    public DbSet<PriorAuthorizationMember> PriorAuthorizationMembers => Set<PriorAuthorizationMember>();

    public DbSet<PriorAuthorizationRequestedService> PriorAuthorizationRequestedServices => Set<PriorAuthorizationRequestedService>();

    public DbSet<PriorAuthorizationRequestingProvider> PriorAuthorizationRequestingProviders => Set<PriorAuthorizationRequestingProvider>();

    public DbSet<PriorAuthorizationQuestionnaireAssignment> PriorAuthorizationQuestionnaireAssignments => Set<PriorAuthorizationQuestionnaireAssignment>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(IntakeDbContext).Assembly);
    }
}
