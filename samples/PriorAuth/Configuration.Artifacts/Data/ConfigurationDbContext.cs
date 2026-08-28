using Kaleido.Samples.PriorAuth.Configuration.Artifacts.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Configuration.Artifacts.Data;

public sealed class ConfigurationDbContext(
    DbContextOptions<ConfigurationDbContext> options) : DbContext(options)
{
    public DbSet<ProcedureModalityRule> ProcedureModalityRules => Set<ProcedureModalityRule>();

    public DbSet<MriProcedureCodeRule> MriProcedureCodeRules => Set<MriProcedureCodeRule>();

    public DbSet<QuestionnaireDefinition> QuestionnaireDefinitions => Set<QuestionnaireDefinition>();

    public DbSet<QuestionnaireDefinitionItem> QuestionnaireDefinitionItems => Set<QuestionnaireDefinitionItem>();

    public DbSet<QuestionnaireDefinitionItemAnswerOption> QuestionnaireDefinitionItemAnswerOptions => Set<QuestionnaireDefinitionItemAnswerOption>();

    public DbSet<QuestionnaireDefinitionItemEnableWhen> QuestionnaireDefinitionItemEnableWhen => Set<QuestionnaireDefinitionItemEnableWhen>();

    public DbSet<QuestionnaireMappingRule> QuestionnaireMappingRules => Set<QuestionnaireMappingRule>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ConfigurationDbContext).Assembly);
    }
}
