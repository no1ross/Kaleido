using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kaleido.Process.Participant;

public enum ProcessStepOutcome
{
    Pending,
    Completed,
    Failed,
    Blocked,
    Cancelled
}
public enum ProcessStepStatus
{
    Pending,
    Completed,
    ValidationFailed,
    Failed,
    Exception
}

public enum ProcessStepMessageType
{
    Information,
    Warning,
    Error
}

public enum ProcessStepMessageCode
{
    UnknownStep,
    InvalidRequest,
    PropertyNotFound,
    ConversionFailed,
    ValidationFailed,
    AlreadyProcessed,
    ConsistencyViolation,
    DependencyNotSatisfied,
    DependencySatisfied
}

internal enum ExecutionCandidateStatus
{
    Pending,
    Built,
    Invalid,
    Satisfied
}