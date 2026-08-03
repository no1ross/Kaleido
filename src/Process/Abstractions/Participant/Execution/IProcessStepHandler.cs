using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kaleido.Process.Participant.Execution;

public interface IProcessStepHandler<in TProcessStep, TProcessStepResult>
{
    Task<ProcessStepHandlerResult<TProcessStepResult>> ExecuteAsync(
        TProcessStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default);
}

public interface IProcessStepHandler<in TProcessStep> : IProcessStepHandler<TProcessStep, ProcessStepEmptyResponse>
{
}

public sealed record ProcessStepEmptyResponse;


