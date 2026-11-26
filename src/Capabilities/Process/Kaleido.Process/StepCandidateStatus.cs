namespace Kaleido.Process;

/// <summary>
/// How a submitted step was planned: the audit vocabulary of the <c>PlanBuilt</c> process event,
/// which carries it per candidate as <c>CandidateStatus</c> (#536).
/// </summary>
/// <remarks>
/// Reporting reads it to see which submitted steps entered the plan and why the others did not. The
/// transport layers' answers do not carry it: a step that did not run is
/// <see cref="StepExecutionStatus.Pending"/> there, and the framework messages say why.
/// </remarks>
public enum StepCandidateStatus
{
    /// <summary>
    /// Not yet planned; a published candidate is never in this status.
    /// </summary>
    Pending,

    /// <summary>
    /// Built from the request and valid: it entered the execution plan unless a later check (not a
    /// next step) kept it out.
    /// </summary>
    Built,

    /// <summary>
    /// Rejected: not registered, unreadable, invalid input, a missing dependency, or not a next step.
    /// The candidate's messages say which.
    /// </summary>
    Invalid,

    /// <summary>
    /// Already completed and not repeatable: it was not run again.
    /// </summary>
    Satisfied,
}
