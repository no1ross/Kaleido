using Kaleido.Queryable.AspNetCore.Client;
using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Samples.PriorAuth.Configuration;
using Kaleido.Samples.PriorAuth.Configuration.Process.Models;
using Kaleido.Samples.PriorAuth.Radiology.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Process.Models;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Services;

public sealed class QuestionnaireDefinitionClient(
    IKaleidoQueryableClientFactory queryableClientFactory,
    IConfiguration configuration,
    RadiologyDbContext dbContext)
{
    private readonly string questionnaireDefinitionView =
        configuration["Services:Configuration:QuestionnaireDefinitionView"]
        ?? "QuestionnaireDefinition";

    public async Task<CaptureRequestedServiceResponse?> ResolveAsync(
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
            .QueryViewAsync<QuestionnaireDefinitionParameters, QuestionnaireDefinitionRecord>(
                "QuestionnaireDefinitions",
                questionnaireDefinitionView,
                new QueryApiRequest<QuestionnaireDefinitionParameters>
                {
                    Parameters = new QuestionnaireDefinitionParameters
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

        return new CaptureRequestedServiceResponse
        {
            QuestionnaireId = questionnaire.QuestionnaireId,
            QuestionnaireVersion = questionnaire.Version,
            Questionnaire = questionnaire
        };
    }
}
