using System.Text.Json;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.ViewSources.Parameters;
using Kaleido.Samples.PriorAuth.Questionnaires;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Services;

/// <summary>
/// Radiology's information requests from the Configuration service: the questionnaire is selected
/// by step name and the request's plan, line of business, modality and procedure, and translated
/// generically (<see cref="QuestionnaireTranslation"/>). Radiology owns no question, no item id and
/// no wording; the business configures them.
/// </summary>
/// <remarks>
/// A question whose binding key matches a detail the caller stated up front about the service being
/// asked about (<see cref="PriorAuthorizationRequestedService.StatedDetailsJson"/>) proposes that
/// value; the caller still confirms it. Every presented request and its answers are kept per process
/// and key (a step name, or a step and clinical round) for review.
/// </remarks>
public sealed class ConfiguredInformationRequests(
    ConfiguredQuestionnaireClient questionnaires,
    RadiologyDbContext dbContext)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The configured request for <paramref name="stepName"/> on this process, or null when none is configured.</summary>
    public async Task<InformationRequest?> CreateAsync(
        Guid processId,
        string stepName,
        ProcedureModality? procedureModality,
        string? procedureCodeValue,
        CancellationToken cancellationToken = default)
    {
        var priorAuthorization = await LoadAsync(processId, cancellationToken);

        var questionnaire = await questionnaires.FindAsync(
            new QuestionnaireDefinitionViewParameters
            {
                StepName = stepName,
                PlanId = priorAuthorization?.Member?.PlanId,
                LineOfBusiness = priorAuthorization?.Member?.LineOfBusiness.ToString(),
                ProcedureModality = procedureModality,
                ProcedureCodeValue = procedureCodeValue
            },
            cancellationToken);

        if (questionnaire is null)
        {
            return null;
        }

        var request = QuestionnaireTranslation.ToInformationRequest(
            questionnaire,
            StatedDetails(priorAuthorization),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["ProcedureCode"] = procedureCodeValue ?? string.Empty });

        await RecordRequestAsync(processId, stepName, questionnaire.QuestionnaireId, questionnaire.Version, request, cancellationToken);

        return request;
    }

    /// <summary>What the caller stated about the service captured last (binding key → value).</summary>
    public async Task<IReadOnlyDictionary<string, string>> StatedDetailsAsync(
        Guid processId,
        CancellationToken cancellationToken = default) =>
        StatedDetails(await LoadAsync(processId, cancellationToken));

    /// <summary>Keeps a presented request under <paramref name="key"/> (clearing earlier answers).</summary>
    public async Task RecordRequestAsync(
        Guid processId,
        string key,
        string questionnaireId,
        string version,
        InformationRequest request,
        CancellationToken cancellationToken = default)
    {
        var assignment =
            await dbContext.PriorAuthorizationQuestionnaireAssignments
                .SingleOrDefaultAsync(x => x.ProcessId == processId && x.StepName == key, cancellationToken);

        if (assignment is null)
        {
            assignment = new PriorAuthorizationQuestionnaireAssignment
            {
                PriorAuthorizationQuestionnaireAssignmentId = Guid.NewGuid(),
                ProcessId = processId,
                StepName = key
            };

            dbContext.PriorAuthorizationQuestionnaireAssignments.Add(assignment);
        }

        assignment.QuestionnaireId = questionnaireId;
        assignment.QuestionnaireVersion = version;
        assignment.Title = request.Title;
        assignment.AssignedUtc = DateTimeOffset.UtcNow;
        assignment.RequestJson = JsonSerializer.Serialize(request, JsonOptions);
        assignment.AnswersJson = null;
        assignment.AnsweredUtc = null;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Keeps the caller's answers with the questions they answer, for review.</summary>
    public async Task RecordAnswersAsync(
        Guid processId,
        string key,
        IInformationStep answers,
        CancellationToken cancellationToken = default)
    {
        var assignment =
            await dbContext.PriorAuthorizationQuestionnaireAssignments
                .SingleOrDefaultAsync(x => x.ProcessId == processId && x.StepName == key, cancellationToken);

        if (assignment is null)
        {
            return;
        }

        var questions = assignment.RequestJson is null
            ? []
            : Flatten(JsonSerializer.Deserialize<InformationRequest>(assignment.RequestJson, JsonOptions)?.Items ?? []).ToArray();

        var answered = answers.Items
            .Select(item =>
            {
                var question = questions.FirstOrDefault(x => string.Equals(x.Id, item.ItemId, StringComparison.Ordinal));

                return new RecordedAnswer(
                    item.ItemId,
                    question?.Text ?? item.ItemId,
                    item.Answers
                        .Select(answer => question?.Options.FirstOrDefault(o => string.Equals(o.Value, answer.Value, StringComparison.Ordinal))?.Display ?? answer.Value)
                        .ToArray());
            })
            .ToArray();

        assignment.AnswersJson = JsonSerializer.Serialize(answered, JsonOptions);
        assignment.AnsweredUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Every answered questionnaire of the process, in the order it was answered.</summary>
    public async Task<IReadOnlyList<RecordedQuestionnaire>> AnswersAsync(
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        var assignments =
            await dbContext.PriorAuthorizationQuestionnaireAssignments
                .AsNoTracking()
                .Where(x => x.ProcessId == processId && x.AnswersJson != null)
                .ToListAsync(cancellationToken);

        return assignments
            .OrderBy(x => x.AnsweredUtc)
            .Select(x => new RecordedQuestionnaire(
                x.StepName,
                x.Title ?? x.QuestionnaireId,
                JsonSerializer.Deserialize<RecordedAnswer[]>(x.AnswersJson ?? "[]", JsonOptions) ?? []))
            .ToArray();
    }

    /// <summary>The first answer to the question with <paramref name="bindingKey"/>.</summary>
    public static string? Answer(
        IInformationStep answers,
        string bindingKey) =>
        QuestionnaireTranslation.Answer(answers, bindingKey);

    /// <summary>Stated details as stored with a requested service (binding key → value).</summary>
    public static string? SerializeStatedDetails(
        IReadOnlyDictionary<string, string?> details)
    {
        var stated = details
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .ToDictionary(x => x.Key, x => x.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        return stated.Count == 0 ? null : JsonSerializer.Serialize(stated, JsonOptions);
    }

    private Task<PriorAuthorization?> LoadAsync(
        Guid processId,
        CancellationToken cancellationToken) =>
        dbContext.PriorAuthorizations
            .AsNoTracking()
            .Include(x => x.Member)
            .Include(x => x.RequestedServices)
            .SingleOrDefaultAsync(x => x.ProcessId == processId, cancellationToken);

    // The questions are about the service captured last; what the caller stated about it is proposed.
    private static IReadOnlyDictionary<string, string> StatedDetails(
        PriorAuthorization? priorAuthorization)
    {
        var json = priorAuthorization?.RequestedServices.MaxBy(x => x.CreatedUtc)?.StatedDetailsJson;

        return string.IsNullOrWhiteSpace(json)
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(
                JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? [],
                StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<InformationItem> Flatten(
        IEnumerable<InformationItem> items) =>
        items.SelectMany(x => new[] { x }.Concat(Flatten(x.Items)));
}

/// <summary>One answered question, as kept for review.</summary>
public sealed record RecordedAnswer(
    string ItemId,
    string Question,
    IReadOnlyList<string> Answers);

/// <summary>The answers given to one presented questionnaire (a step, or a clinical round).</summary>
public sealed record RecordedQuestionnaire(
    string Key,
    string Title,
    IReadOnlyList<RecordedAnswer> Answers);
