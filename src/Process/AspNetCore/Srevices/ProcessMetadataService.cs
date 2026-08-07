using Kaleido.Process.AspNetCore.Contracts;

namespace Kaleido.Process.AspNetCore.Srevices;

public interface IProcessMetadataService
{
    Task<IReadOnlyCollection<ProcessStepSummary>> GetProcessesAsync(
        CancellationToken cancellationToken);
}
