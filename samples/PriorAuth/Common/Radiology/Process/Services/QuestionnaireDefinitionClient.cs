using Kaleido.Http.Queryable;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.ViewSources.Parameters;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.ViewSources.Views;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Data.Entities;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Services;

/// <summary>
/// Builds the information requests the radiology steps present. The questions are the domain's:
/// the MRI questions come from the Configuration service (per plan, line of business and
/// modality); the CT confirmation is radiology's own wording. Kaleido only carries them.
/// </summary>
/// <remarks>
/// Interim: the MRI mapping and the CT confirmation are specific to this sample. #209 replaces
/// them with a generic, configuration-driven builder for any step.
/// </remarks>
public sealed class QuestionnaireDefinitionClient(
    IKaleidoQueryableClientFactory queryableClientFactory,
    RadiologyDbContext dbContext)
{
    public const string CtConfirmationRequestId = "priorauth-ct-confirmation";

    public const string CtConfirmationItemId = "confirm-ct";

    /// <summary>The configured MRI questions for this process, or null when none is configured.</summary>
    public async Task<InformationRequest?> CreateMriInfoRequestAsync(
        Guid processId,
        string procedureCodeValue,
        CancellationToken cancellationToken = default)
    {
        var questionnaire =
            await ResolveAsync(
                processId,
                nameof(CaptureMriInfoStep),
                ProcedureModality.Mri,
                procedureCodeValue,
                cancellationToken);

        return questionnaire is null
            ? null
            : ToInformationRequest(questionnaire);
    }

    public InformationRequest CreateCtConfirmationRequest(
        string procedureCodeValue) =>
        new()
        {
            InformationRequestId = CtConfirmationRequestId,
            Title = "Confirm CT",
            Items =
            [
                new InformationItem
                {
                    Id = CtConfirmationItemId,
                    Text = $"Procedure {procedureCodeValue} is a CT scan. Confirm that a CT, not an MRI, is being requested.",
                    Type = InformationItemType.Boolean
                }
            ]
        };

    private async Task<QuestionnaireDefinitionView?> ResolveAsync(
        Guid processId,
        string stepName,
        ProcedureModality procedureModality,
        string? procedureCodeValue,
        CancellationToken cancellationToken)
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
                "QuestionnaireDefinitionQueryContextSource",
                "QuestionnaireDefinitionViewSource",
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

    // The configuration's own format (FHIR-like, with bindings and enableWhen) is translated into
    // Kaleido's information request; conditions the subset doesn't carry are dropped.
    private static InformationRequest ToInformationRequest(
        QuestionnaireDefinitionView questionnaire) =>
        new()
        {
            InformationRequestId = $"{questionnaire.QuestionnaireId}|{questionnaire.Version}",
            Title = questionnaire.Title,
            Items =
                questionnaire.Items
                    .OrderBy(x => x.Order)
                    .Select(item => new InformationItem
                    {
                        Id = item.LinkId,
                        Text = item.Text,
                        Type = ToItemType(item.Type),
                        Repeats = item.Repeats,
                        Options =
                            item.AnswerOptions
                                .OrderBy(x => x.Order)
                                .Select(option => new InformationOption { Value = option.Value, Display = option.DisplayText })
                                .ToArray()
                    })
                    .ToArray()
        };

    private static InformationItemType ToItemType(
        string type) =>
        type.ToLowerInvariant() switch
        {
            "choice" => InformationItemType.Choice,
            "boolean" => InformationItemType.Boolean,
            "integer" => InformationItemType.WholeNumber,
            "decimal" => InformationItemType.Number,
            "date" => InformationItemType.Date,
            "datetime" => InformationItemType.DateTime,
            "text" => InformationItemType.LongText,
            "display" => InformationItemType.Display,
            "group" => InformationItemType.Group,
            _ => InformationItemType.Text
        };
}
