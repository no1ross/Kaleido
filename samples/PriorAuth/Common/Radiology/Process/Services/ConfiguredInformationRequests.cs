using System.Text.Json;
using Kaleido.Http.Queryable;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.ViewSources.Parameters;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.ViewSources.Views;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Services;

/// <summary>
/// Builds every radiology information request from the Configuration service: the questionnaire
/// is selected by step name and the request's plan, line of business, modality and procedure, and
/// translated generically into Kaleido's information request. Radiology owns no question, no item
/// id and no wording; the business configures them.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><b>Ids are binding keys.</b> An item's id is its configured <c>BindingKey</c>
/// (its <c>LinkId</c> when it has none), so handlers read answers by the business's keys.</description></item>
/// <item><description><b>Proposed answers.</b> A question whose binding key matches a detail the
/// caller stated up front about the service being asked about
/// (<see cref="PriorAuthorizationRequestedService.StatedDetailsJson"/>) proposes that value when it
/// fits; the caller still confirms it. Configured defaults are never proposed.</description></item>
/// <item><description><b>Tokens.</b> Question and title text may contain tokens filled from the
/// request (e.g. <c>{ProcedureCode}</c>).</description></item>
/// <item><description><b>Answers are kept</b> with the questions (text and option display) per process
/// and step, for review.</description></item>
/// </list>
/// </remarks>
public sealed class ConfiguredInformationRequests(
    IKaleidoQueryableClientFactory queryableClientFactory,
    RadiologyDbContext dbContext)
{
    private const string ConfigurationClient = "Configuration";

    /// <summary>The configured request for <paramref name="stepName"/> on this process, or null when none is configured.</summary>
    public async Task<InformationRequest?> CreateAsync(
        Guid processId,
        string stepName,
        ProcedureModality? procedureModality,
        string? procedureCodeValue,
        CancellationToken cancellationToken = default)
    {
        var priorAuthorization =
            await dbContext.PriorAuthorizations
                .AsNoTracking()
                .Include(x => x.Member)
                .Include(x => x.RequestedServices)
                .SingleOrDefaultAsync(x => x.ProcessId == processId, cancellationToken);

        // The questions are about the service captured last; what the caller stated about it is proposed.
        var latestService = priorAuthorization?.RequestedServices.MaxBy(x => x.CreatedUtc);

        var result = await queryableClientFactory
            .GetClient(ConfigurationClient)
            .QueryViewAsync<QuestionnaireDefinitionViewParameters, QuestionnaireDefinitionView>(
                "QuestionnaireDefinitionQueryContextSource",
                "QuestionnaireDefinitionViewSource",
                new QueryApiRequest<QuestionnaireDefinitionViewParameters>
                {
                    Parameters = new QuestionnaireDefinitionViewParameters
                    {
                        StepName = stepName,
                        PlanId = priorAuthorization?.Member?.PlanId,
                        LineOfBusiness = priorAuthorization?.Member?.LineOfBusiness.ToString(),
                        ProcedureModality = procedureModality,
                        ProcedureCodeValue = procedureCodeValue
                    }
                },
                cancellationToken);

        if (result.Results.SingleOrDefault() is not { } questionnaire)
        {
            return null;
        }

        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProcedureCode"] = procedureCodeValue ?? string.Empty
        };

        var request = ToInformationRequest(questionnaire, StatedDetails(latestService?.StatedDetailsJson), tokens);

        await AssignAsync(processId, stepName, questionnaire, request, cancellationToken);

        return request;
    }

    /// <summary>Keeps the caller's answers with the questions they answer, for review.</summary>
    public async Task RecordAnswersAsync(
        Guid processId,
        string stepName,
        IInformationStep answers,
        CancellationToken cancellationToken = default)
    {
        var assignment =
            await dbContext.PriorAuthorizationQuestionnaireAssignments
                .SingleOrDefaultAsync(x => x.ProcessId == processId && x.StepName == stepName, cancellationToken);

        if (assignment is null)
        {
            return;
        }

        var questions = assignment.RequestJson is null
            ? []
            : JsonSerializer.Deserialize<InformationRequest>(assignment.RequestJson, JsonOptions)?.Items ?? [];

        var answered = answers.Items
            .Select(item =>
            {
                var question = Flatten(questions).FirstOrDefault(x => string.Equals(x.Id, item.ItemId, StringComparison.Ordinal));

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

    /// <summary>The recorded answers of every answered step of the process, in the order they were asked.</summary>
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
        answers.Items
            .FirstOrDefault(x => string.Equals(x.ItemId, bindingKey, StringComparison.OrdinalIgnoreCase))?
            .Answers
            .FirstOrDefault()?
            .Value;

    /// <summary>Stated details as stored on the request (binding key → value).</summary>
    public static string? SerializeStatedDetails(
        IReadOnlyDictionary<string, string?> details)
    {
        var stated = details
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .ToDictionary(x => x.Key, x => x.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        return stated.Count == 0 ? null : JsonSerializer.Serialize(stated, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private async Task AssignAsync(
        Guid processId,
        string stepName,
        QuestionnaireDefinitionView questionnaire,
        InformationRequest request,
        CancellationToken cancellationToken)
    {
        var assignment =
            await dbContext.PriorAuthorizationQuestionnaireAssignments
                .SingleOrDefaultAsync(x => x.ProcessId == processId && x.StepName == stepName, cancellationToken);

        if (assignment is null)
        {
            assignment = new PriorAuthorizationQuestionnaireAssignment
            {
                PriorAuthorizationQuestionnaireAssignmentId = Guid.NewGuid(),
                ProcessId = processId,
                StepName = stepName
            };

            dbContext.PriorAuthorizationQuestionnaireAssignments.Add(assignment);
        }

        assignment.QuestionnaireId = questionnaire.QuestionnaireId;
        assignment.QuestionnaireVersion = questionnaire.Version;
        assignment.Title = questionnaire.Title;
        assignment.AssignedUtc = DateTimeOffset.UtcNow;
        assignment.RequestJson = JsonSerializer.Serialize(request, JsonOptions);
        assignment.AnswersJson = null;
        assignment.AnsweredUtc = null;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // The configuration's format (FHIR-like, with bindings and enableWhen) translated into Kaleido's
    // information request. Conditions are not carried yet (#235).
    private static InformationRequest ToInformationRequest(
        QuestionnaireDefinitionView questionnaire,
        IReadOnlyDictionary<string, string> stated,
        IReadOnlyDictionary<string, string> tokens) =>
        new()
        {
            InformationRequestId = $"{questionnaire.QuestionnaireId}|{questionnaire.Version}",
            Title = Fill(questionnaire.Title, tokens),
            Items =
                questionnaire.Items
                    .OrderBy(x => x.Order)
                    .Select(item => ToItem(item, stated, tokens))
                    .ToArray()
        };

    private static InformationItem ToItem(
        QuestionnaireItemView item,
        IReadOnlyDictionary<string, string> stated,
        IReadOnlyDictionary<string, string> tokens)
    {
        var type = ToItemType(item.Type);

        var options = item.AnswerOptions
            .OrderBy(x => x.Order)
            .Select(option => new InformationOption { Value = option.Value, Display = option.DisplayText })
            .ToArray();

        var proposal =
            !string.IsNullOrWhiteSpace(item.BindingKey) &&
            stated.TryGetValue(item.BindingKey, out var value) &&
            Fits(type, options, value)
                ? value
                : null;

        return new InformationItem
        {
            Id = string.IsNullOrWhiteSpace(item.BindingKey) ? item.LinkId : item.BindingKey,
            Text = Fill(item.Text, tokens),
            Type = type,
            Repeats = item.Repeats,
            Options = options,
            ProposedAnswers = proposal is null ? [] : [new InformationAnswer { Value = proposal }]
        };
    }

    // Only a value the caller could submit unchanged is proposed.
    private static bool Fits(
        InformationItemType type,
        IReadOnlyList<InformationOption> options,
        string value) =>
        type switch
        {
            InformationItemType.Choice => options.Any(x => string.Equals(x.Value, value, StringComparison.Ordinal)),
            InformationItemType.Boolean => bool.TryParse(value, out _),
            InformationItemType.Display or InformationItemType.Group => false,
            _ => !string.IsNullOrWhiteSpace(value)
        };

    private static string Fill(
        string? text,
        IReadOnlyDictionary<string, string> tokens) =>
        tokens.Aggregate(
            text ?? string.Empty,
            (current, token) => current.Replace($"{{{token.Key}}}", token.Value, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyDictionary<string, string> StatedDetails(
        string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(
                JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? [],
                StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<InformationItem> Flatten(
        IEnumerable<InformationItem> items) =>
        items.SelectMany(x => new[] { x }.Concat(Flatten(x.Items)));

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

/// <summary>One answered question, as kept for review.</summary>
public sealed record RecordedAnswer(
    string ItemId,
    string Question,
    IReadOnlyList<string> Answers);

/// <summary>The answers given to one step's questionnaire.</summary>
public sealed record RecordedQuestionnaire(
    string StepName,
    string Title,
    IReadOnlyList<RecordedAnswer> Answers);
