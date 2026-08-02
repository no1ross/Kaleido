using Kaleido.Process.Participant.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kaleido.Process.Participant.Execution;

public interface IProcessStepHandler<in TProcessStep>
{
    Task<ProcessStepResult> ExecuteAsync(
        TProcessStep processStep,
        StepContext context,
        CancellationToken cancellationToken = default);
}
