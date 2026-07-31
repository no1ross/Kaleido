using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kaleido.Process.Participant;

public interface IParticipantService
{
    Task<ProcessStepResult> ExecuteAsync<TProcessStep>(
        TProcessStep processStep,
        CancellationToken cancellationToken = default);
}
