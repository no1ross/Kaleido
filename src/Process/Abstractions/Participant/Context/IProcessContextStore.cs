using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kaleido.Process.Participant.Context;

internal interface IProcessContextStore
{
    Task<ParticipantContext> LoadAsync(string? correlationId, CancellationToken cancellationToken = default);

    Task SaveAsync(ParticipantContext context, CancellationToken cancellationToken = default);
}
