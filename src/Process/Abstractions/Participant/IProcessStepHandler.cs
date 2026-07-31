using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kaleido.Process.Participant;

public interface IProcessStepHandler<in TProcessStep>
{
    Task<ProcessStepResult> ExecuteAsync(
        TProcessStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default);
}
