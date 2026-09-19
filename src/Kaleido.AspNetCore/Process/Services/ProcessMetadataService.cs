using Kaleido.Process.AspNetCore.Contracts;

namespace Kaleido.Process.AspNetCore.Services;

public interface IProcessMetadataService
{
    Task<IReadOnlyCollection<ProcessStepSummary>> GetProcessesAsync(
        CancellationToken cancellationToken);
}
