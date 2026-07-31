using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kaleido.Process.Participant;

public enum ProcessStepOutcome
{
    Succeeded,
    Failed,
    Blocked,
    Completed,
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