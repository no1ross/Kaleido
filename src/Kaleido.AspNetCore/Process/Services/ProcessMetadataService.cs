using Kaleido.Http.Abstractions.Process.Contracts;

namespace Kaleido.AspNetCore.Process.Services;

public interface IProcessMetadataService
{
    Task<IReadOnlyCollection<ProcessStepSummary>> GetProcessesAsync(
        CancellationToken cancellationToken);
}
