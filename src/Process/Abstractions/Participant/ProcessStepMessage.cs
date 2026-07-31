using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kaleido.Process.Participant;

public sealed record ProcessStepMessage
{
    public ProcessStepMessageType Severity { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? Code { get; init; }

    public static ProcessStepMessage Information(
        string message,
        string? code = null)
    {
        return new()
        {
            Severity = ProcessStepMessageType.Information,
            Message = message,
            Code = code
        };
    }

    public static ProcessStepMessage Warning(
        string message,
        string? code = null)
    {
        return new()
        {
            Severity = ProcessStepMessageType.Warning,
            Message = message,
            Code = code
        };
    }

    public static ProcessStepMessage Error(
        string message,
        string? code = null)
    {
        return new()
        {
            Severity = ProcessStepMessageType.Error,
            Message = message,
            Code = code
        };
    }
}


