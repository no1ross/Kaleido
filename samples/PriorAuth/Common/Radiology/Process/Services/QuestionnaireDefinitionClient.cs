using Kaleido.Http.Queryable.Contracts;
using Kaleido.Samples.PriorAuth.Configuration;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.ViewSources.Parameters;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.ViewSources.Views;
using Kaleido.Samples.PriorAuth.Radiology.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Process.Models;
using Kaleido.Http.Queryable;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Services;

public sealed class QuestionnaireDefinitionClient(
    IKaleidoQueryableClientFactory queryableClientFactory,
    RadiologyDbContext dbContext)
{
    public async Task<QuestionnaireDefinitionView?> ResolveAsync(
        Guid processId,
        string stepName,
        ProcedureModality procedureModality,
        string? procedureCodeValue,
        CancellationToken cancellationToken = default)
    {
        var member =
            await dbContext.PriorAuthorizations
                .AsNoTracking()
                .Where(x => x.ProcessId == processId)
                .Select(x => x.Member)
                .SingleOrDefaultAsync(cancellationToken);

        var result = await queryableClientFactory
            .GetClient("Configuration")
            .QueryViewAsync<QuestionnaireDefinitionViewParameters, QuestionnaireDefinitionView>(
                "questionnaire-definitions",
                "questionnaire-definition",
                new QueryApiRequest<QuestionnaireDefinitionViewParameters>
                {
                    Parameters = new QuestionnaireDefinitionViewParameters
                    {
                        StepName = stepName,
                        PlanId = member?.PlanId,
                        LineOfBusiness = member?.LineOfBusiness.ToString(),
                        ProcedureModality = procedureModality,
                        ProcedureCodeValue = procedureCodeValue
                    }
                },
                cancellationToken);

        var questionnaire = result.Results.SingleOrDefault();

        if (questionnaire is null)
        {
            return null;
        }

        var assignment =
            await dbContext.PriorAuthorizationQuestionnaireAssignments
                .SingleOrDefaultAsync(
                    x => x.ProcessId == processId && x.StepName == stepName,
                    cancellationToken);

        if (assignment is null)
        {
            assignment = new PriorAuthorizationQuestionnaireAssignment
            {
                PriorAuthorizationQuestionnaireAssignmentId = Guid.NewGuid(),
                ProcessId = processId,
                StepName = stepName,
                QuestionnaireId = questionnaire.QuestionnaireId,
                QuestionnaireVersion = questionnaire.Version,
                AssignedUtc = DateTimeOffset.UtcNow
            };

            dbContext.PriorAuthorizationQuestionnaireAssignments.Add(assignment);
        }
        else
        {
            assignment.QuestionnaireId = questionnaire.QuestionnaireId;
            assignment.QuestionnaireVersion = questionnaire.Version;
            assignment.AssignedUtc = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return questionnaire;
    }
}
