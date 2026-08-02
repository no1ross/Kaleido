using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kaleido.Process.Participant.Planning;



/// <summary>
/// Represents the state of a step candidate during the planning phase.
/// </summary>
public enum StepCandidateStatus
{
    /// <summary>
    /// Initial state before any planning or validation has occurred.
    /// </summary>
    Pending,

    /// <summary>
    /// The candidate was successfully created from the request
    /// and is eligible for validation and consistency checks.
    /// </summary>
    Built,

    /// <summary>
    /// The candidate failed validation or consistency checks
    /// and cannot participate in the execution plan.
    /// </summary>
    Invalid,

    /// <summary>
    /// The step has already been completed for the participant
    /// and does not need to be included in the execution plan.
    /// </summary>
    Satisfied
}