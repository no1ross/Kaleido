using Kaleido.Http.Abstractions.Process.Contracts;

namespace Kaleido.Http.Process.Services;

public interface IProcessMetadataService
{
    Task<IReadOnlyCollection<ProcessStepSummary>> GetProcessesAsync(
        CancellationToken cancellationToken);
}
