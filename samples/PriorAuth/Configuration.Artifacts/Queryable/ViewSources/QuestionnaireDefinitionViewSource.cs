using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.Configuration.Data;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.ViewSources.Parameters;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.ViewSources.Views;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Configuration.Queryable.ViewSources;

[QueryView(
    Name = "questionnaire-definition",
    DisplayName = "Questionnaire Definition",
    Version = "1.0.0",
    Description = "Resolves a questionnaire definition for a given step and business context.")]
internal sealed class QuestionnaireDefinitionViewSource(
    ConfigurationDbContext dbContext)
    : IQueryViewSource<
        QuestionnaireDefinitionQueryContext,
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

        if (string.IsNullOrWhiteSpace(parameters.StepName))
        {
            return Enumerable.Empty<QuestionnaireDefinitionView>().AsQueryable();
        }

        var matchedDefinitionIds =
            dbContext.QuestionnaireMappingRules
                .AsNoTracking()
                .Where(rule =>
                    rule.IsActive
                    && rule.StepName == parameters.StepName
                    && (rule.PlanId == null || rule.PlanId == parameters.PlanId)
                    && (rule.LineOfBusiness == null || rule.LineOfBusiness == parameters.LineOfBusiness)
                    && (rule.ProcedureModality == null || rule.ProcedureModality == parameters.ProcedureModality)
                    && (rule.ProcedureCodeValue == null || rule.ProcedureCodeValue == parameters.ProcedureCodeValue))
                .OrderByDescending(rule =>
                    (rule.PlanId != null ? 8 : 0)
                    + (rule.LineOfBusiness != null ? 4 : 0)
                    + (rule.ProcedureCodeValue != null ? 2 : 0)
                    + (rule.ProcedureModality != null ? 1 : 0))
                .ThenByDescending(rule => rule.Priority)
                .Select(rule => rule.QuestionnaireDefinitionId)
                .Take(1);

        return dbContext.QuestionnaireDefinitions
            .AsNoTracking()
            .Where(definition =>
                definition.IsActive
                && matchedDefinitionIds.Contains(definition.QuestionnaireDefinitionId))
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
}
