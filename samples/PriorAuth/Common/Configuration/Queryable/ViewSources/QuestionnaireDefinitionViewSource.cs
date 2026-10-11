using Kaleido.Queryable;
using Kaleido.Samples.PriorAuth.Configuration.Data;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.ContextSources;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.ViewSources.Parameters;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.ViewSources.Views;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Configuration.Queryable.ViewSources;

[QueryView(
    DisplayName = "Questionnaire Definition",
    Version = "1.0.0",
    Description = "Resolves a questionnaire definition for a given step and business context.")]
internal sealed class QuestionnaireDefinitionViewSource(
    ConfigurationDbContext dbContext)
    : IQueryViewSource<
        QuestionnaireDefinitionQueryContextSource, QuestionnaireDefinitionQueryContext,
        QuestionnaireDefinitionView,
        QuestionnaireDefinitionViewParameters>
{
    public IQueryable<QuestionnaireDefinitionView> CreateView(
        IQueryable<QuestionnaireDefinitionQueryContext> query,
        QueryExecutionContext executionContext)
    {
        var parameters =
            executionContext.TryGetViewParameters<QuestionnaireDefinitionViewParameters>()
            ?? new QuestionnaireDefinitionViewParameters();

        if (!string.IsNullOrWhiteSpace(parameters.QuestionnaireId))
        {
            return Project(dbContext.QuestionnaireDefinitions
                .AsNoTracking()
                .Where(definition => definition.IsActive && definition.QuestionnaireId == parameters.QuestionnaireId));
        }

        if (string.IsNullOrWhiteSpace(parameters.StepName))
        {
            return Enumerable.Empty<QuestionnaireDefinitionView>().AsQueryable();
        }

        var matchedDefinitionIds =
            dbContext.QuestionnaireMappingRules
                .AsNoTracking()
                // A rule matches the step, and each criterion it sets (unset criteria match anything).
                .Where(rule => rule.IsActive && rule.StepName == parameters.StepName)
                .Where(rule => rule.PlanId == null || rule.PlanId == parameters.PlanId)
                .Where(rule => rule.LineOfBusiness == null || rule.LineOfBusiness == parameters.LineOfBusiness)
                .Where(rule => rule.ProcedureModality == null || rule.ProcedureModality == parameters.ProcedureModality)
                .Where(rule => rule.ProcedureCodeValue == null || rule.ProcedureCodeValue == parameters.ProcedureCodeValue)
                .OrderByDescending(rule =>
                    (rule.PlanId != null ? 8 : 0)
                    + (rule.LineOfBusiness != null ? 4 : 0)
                    + (rule.ProcedureCodeValue != null ? 2 : 0)
                    + (rule.ProcedureModality != null ? 1 : 0))
                .ThenByDescending(rule => rule.Priority)
                .Select(rule => rule.QuestionnaireDefinitionId)
                .Take(1);

        return Project(dbContext.QuestionnaireDefinitions
            .AsNoTracking()
            .Where(definition =>
                definition.IsActive
                && matchedDefinitionIds.Contains(definition.QuestionnaireDefinitionId)));
    }

    private static IQueryable<QuestionnaireDefinitionView> Project(
        IQueryable<Data.Entities.QuestionnaireDefinition> definitions) =>
        // Items, and per item its answer options and enable-when conditions: sibling collections
        // in one projection (EF MultipleCollectionIncludeWarning, #98). Split into one query per
        // collection instead of a cartesian join; scoped to this query only.
        definitions
            .AsSplitQuery()
            .Select(definition =>
                new QuestionnaireDefinitionView
                {
                    QuestionnaireId = definition.QuestionnaireId,
                    Version = definition.Version,
                    Name = definition.Name,
                    Title = definition.Title,
                    Description = definition.Description,
                    Items = definition.Items
                        .OrderBy(item => item.Order)
                        .Select(item =>
                            new QuestionnaireItemView
                            {
                                LinkId = item.LinkId,
                                Text = item.Text,
                                Type = item.Type,
                                BindingKey = item.BindingKey,
                                Required = item.Required,
                                Repeats = item.Repeats,
                                DefaultValue = item.DefaultValue,
                                Order = item.Order,
                                AnswerOptions = item.AnswerOptions
                                    .OrderBy(option => option.Order)
                                    .Select(option =>
                                        new QuestionnaireAnswerOptionView
                                        {
                                            Value = option.Value,
                                            DisplayText = option.DisplayText,
                                            Order = option.Order
                                        })
                                    .ToArray(),
                                EnableWhen = item.EnableWhen
                                    .Select(condition =>
                                        new QuestionnaireEnableWhenView
                                        {
                                            QuestionBindingKey = condition.QuestionBindingKey,
                                            Operator = condition.Operator,
                                            AnswerValue = condition.AnswerValue
                                        })
                                    .ToArray()
                            })
                        .ToArray()
                });
}
