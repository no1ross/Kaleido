using Kaleido.Samples.PriorAuth.Configuration.Artifacts;
using Kaleido.Samples.PriorAuth.Configuration.Artifacts.Process.Models;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Data;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Data.Entities;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Services;

public sealed class QuestionnaireDefinitionClient(
    QueryableHttpClient queryableHttpClient,
    IConfiguration configuration,
    IntakeDbContext dbContext)
{
    private readonly string questionnaireDefinitionViewPath =
        configuration["Services:Configuration:QuestionnaireDefinitionViewPath"]
        ?? "/configuration/queryable/questionnaire-definitions/questionnaire-definition/query";

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

        var questionnaire =
            await queryableHttpClient.QueryAsync<QuestionnaireDefinitionParameters, QuestionnaireDefinitionRecord, QuestionnaireDefinitionRecord?>(
                "Configuration",
                questionnaireDefinitionViewPath,
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
                result => result.Records.SingleOrDefault(),
                cancellationToken);

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
