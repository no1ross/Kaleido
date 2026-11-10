using Kaleido.Processor.Context;

namespace Kaleido.Processor.Execution;

/// <summary>
/// The process and step context the runtime passes to a step handler's <c>ExecuteAsync</c>.
/// Created by the runtime for each step it executes.
/// </summary>
/// <param name="ProcessId">The process instance the step runs in.</param>
/// <param name="StepContext">
/// The step's stored state in the process before this execution: its status, version, and the
/// request, caller and time of its earlier executions, if any.
/// </param>
/// <param name="AvailableNextSteps">
/// The registered steps the process rules make available once this step completes, given the steps
/// completed so far plus this one, computed before the handler runs. Information steps are
/// included; they only run when required with an information request.
/// </param>
/// <remarks>
/// The handler's input is its own step (the typed step instance); it does not see the other steps
/// submitted with the same request.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record ProcessStepContext
(
    Guid ProcessId,
    StepContext StepContext,
    IReadOnlyCollection<string> AvailableNextSteps
);
