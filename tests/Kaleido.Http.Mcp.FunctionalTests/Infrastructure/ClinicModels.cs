using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Http.Mcp.FunctionalTests.Infrastructure;

// A small "clinic" service: a searchable patient source, a doctor-only chart source, and a visit
// process whose first step asks the user questions (an information request) before it can close.

public sealed class PatientQueryContext : IQueryContext
{
    [Key]
    [Description("The patient's id.")]
    [Filterable(FilterOperator.Equals)]
    [Sortable]
    public int Id { get; init; }

    [Description("The patient's full name.")]
    [Searchable(Priority = 1, MatchMode = MatchMode.Contains)]
    [Sortable]
    public string Name { get; init; } = string.Empty;

    [Description("The ward the patient is in.")]
    [Filterable(FilterOperator.Equals, FilterOperator.In)]
    [Sortable]
    public string Ward { get; init; } = string.Empty;

    [Description("The patient's age in years.")]
    [Filterable(FilterOperator.GreaterThanOrEqual)]
    [Sortable]
    public int Age { get; init; }
}

[QuerySource(
    Version = "1.0.0",
    DisplayName = "Patients",
    Description = "Patients registered at the clinic.",
    DefaultSortField = nameof(PatientQueryContext.Id))]
[Pageable(DefaultSize = 10, MaxSize = 10)]
public sealed class PatientSource : IQuerySource<PatientQueryContext>
{
    public IQueryable<PatientQueryContext> CreateQuery(
        QueryExecutionContext executionContext) =>
        new[]
        {
            new PatientQueryContext { Id = 1, Name = "Ava Carter", Ward = "A", Age = 45 },
            new PatientQueryContext { Id = 2, Name = "Ben Ortiz", Ward = "B", Age = 62 },
            new PatientQueryContext { Id = 3, Name = "Cleo Diaz", Ward = "B", Age = 70 },
            new PatientQueryContext { Id = 4, Name = "Dev Patel", Ward = "A", Age = 30 },
            new PatientQueryContext { Id = 5, Name = "Eli Brooks", Ward = "C", Age = 81 }
        }.AsQueryable();
}

public sealed class ChartQueryContext : IQueryContext
{
    [Key]
    [Description("The chart's id.")]
    [Filterable(FilterOperator.Equals)]
    public int Id { get; init; }
}

[QuerySource(
    Version = "1.0.0",
    DisplayName = "Charts",
    Description = "Clinical charts; doctors only.")]
[KaleidoAuthorization(Roles = ClinicRoles.Doctor)]
public sealed class ChartSource : IQuerySource<ChartQueryContext>
{
    public IQueryable<ChartQueryContext> CreateQuery(
        QueryExecutionContext executionContext) =>
        new[] { new ChartQueryContext { Id = 7 } }.AsQueryable();
}

public static class ClinicRoles
{
    public const string Doctor = "doctor";
}

public static class VisitQuestions
{
    public const string RequestId = "visit-intake";
    public const string Reason = "reason";
    public const string Urgent = "urgent";

    public static InformationRequest Request { get; } = new()
    {
        InformationRequestId = RequestId,
        Title = "Visit intake",
        Items =
        [
            new InformationItem
            {
                Id = Reason,
                Text = "Why is the patient visiting today?",
                Type = InformationItemType.Choice,
                Options =
                [
                    new InformationOption { Value = "checkup", Display = "Routine check-up" },
                    new InformationOption { Value = "injury", Display = "Injury" }
                ]
            },
            new InformationItem
            {
                Id = Urgent,
                Text = "Is it urgent?",
                Type = InformationItemType.Boolean
            }
        ]
    };

    // Asked for this patient: what happened, only when the visit is urgent (EnableWhen, ADR 0024).
    public const int ConditionalPatientId = 9;
    public const string WhatHappened = "whatHappened";

    public static InformationRequest Conditional { get; } = Request with
    {
        Items =
        [
            .. Request.Items,
            new InformationItem
            {
                Id = WhatHappened,
                Text = "What happened?",
                Type = InformationItemType.Text,
                EnableWhen = [new InformationCondition { Question = Urgent, Operator = InformationConditionOperator.Equals, Answer = "true" }]
            }
        ]
    };
}

[ProcessStep(Version = "1.0", DisplayName = "Start visit", Description = "Opens a visit for a patient.")]
public sealed record StartVisitStep : IProcessStep
{
    [Required]
    [Description("The id of the patient (from the Patients search).")]
    public int PatientId { get; init; }
}

public sealed class StartVisitHandler : IProcessStepHandler<StartVisitStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(
        StartVisitStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            ProcessStepHandlerResult.RequireInformation<VisitQuestionsStep>(
                processStep.PatientId == VisitQuestions.ConditionalPatientId ? VisitQuestions.Conditional : VisitQuestions.Request));
}

[ProcessStep(Version = "1.0", DisplayName = "Visit questions", Description = "Answers the visit intake questions.")]
public sealed record VisitQuestionsStep : IInformationStep
{
    public string InformationRequestId { get; init; } = string.Empty;

    public IReadOnlyList<InformationResponseItem> Items { get; init; } = [];
}

public sealed class VisitQuestionsHandler : IProcessStepHandler<VisitQuestionsStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(
        VisitQuestionsStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ProcessStepHandlerResult.Success<CloseVisitStep>());
}

[ProcessStep(Version = "1.0", DisplayName = "Close visit", Description = "Closes the visit.", Destructive = true, RequiresConfirmation = true)]
[AvailableAfter<VisitQuestionsStep>]
public sealed record CloseVisitStep : IProcessStep
{
    [Description("A closing note for the visit.")]
    public string? Note { get; init; }
}

// A closing note of "?" is rejected as correctable; otherwise the visit closes with a business
// message for the user.
public sealed class CloseVisitHandler : IProcessStepHandler<CloseVisitStep>
{
    public const string UnclearNote = "?";
    public const string UnclearCode = "CLINIC_NOTE_UNCLEAR";
    public const string UnclearMessage = "The closing note is unclear; write what was done.";
    public const string ClosedMessage = "The visit is closed; the patient may leave.";

    public Task<ProcessStepHandlerResult> ExecuteAsync(
        CloseVisitStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            processStep.Note == UnclearNote
                ? ProcessStepHandlerResult.Reject(new ProcessMessage { Code = UnclearCode, Type = MessageType.Error, Message = UnclearMessage })
                : ProcessStepHandlerResult.Success(new ProcessMessage { Code = "CLINIC_VISIT_CLOSED", Type = MessageType.Information, Message = ClosedMessage }));
}
