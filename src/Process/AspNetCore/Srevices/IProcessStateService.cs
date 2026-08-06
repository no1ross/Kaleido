using Kaleido.Process.AspNetCore.Contracts;

namespace Kaleido.Process.AspNetCore.Srevices;

public interface IProcessStateService
{
    Task<IReadOnlyCollection<ProcessStepSummaryContract>> GetCurrentState(
        string? participantProcessId,
        CancellationToken cancellationToken);
}