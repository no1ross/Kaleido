using Kaleido.Process.AspNetCore.Contracts;

namespace Kaleido.Process.AspNetCore.Srevices;

public interface IProcessStateService
{
    Task<IReadOnlyCollection<ProcessStepSummary>> GetCurrentState(
        string? participantProcessId,
        CancellationToken cancellationToken);
}

public class ProcessStateService : IProcessStateService
{
    public Task<IReadOnlyCollection<ProcessStepSummary>> GetCurrentState(string? participantProcessId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}