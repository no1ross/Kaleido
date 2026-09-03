using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Kaleido.Process.Execution;

/// <summary>
/// Represents the action the execution processor should take
/// after evaluating the outcome of a step execution.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExecutionDecisionType
{
    /// <summary>
    /// Execution can continue immediately using the specified
    /// next candidate.
    /// </summary>
    Continue,

    /// <summary>
    /// The process has reached a terminal state and no further
    /// execution is possible or required.
    /// </summary>
    Complete,

    /// <summary>
    /// The step executed successfully, but the business outcome
    /// indicated failure and execution cannot continue.
    /// </summary>
    BusinessFailure,

    /// <summary>
    /// A process rule or framework invariant was violated.
    /// Examples include illegal graph transitions or invalid
    /// required step selections.
    /// </summary>
    ProcessViolation,

    /// <summary>
    /// Execution cannot continue until a specific next step
    /// is supplied by the consumer.
    /// </summary>
    AwaitingRequiredStep,

    /// <summary>
    /// Execution cannot continue because no executable candidate
    /// was supplied. The consumer must select one of the available
    /// next steps returned by the process.
    /// </summary>
    AwaitingStepSelection
}
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProcessExecutionState
{
    Active,

    Complete,

    BusinessFailure,

    ProcessViolation,

    AwaitingRequiredStep,

    AwaitingStepSelection,

    Exception,

    Cancelled
}
