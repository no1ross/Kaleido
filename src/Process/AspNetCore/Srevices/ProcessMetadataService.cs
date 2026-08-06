using Kaleido.Process.AspNetCore.Contracts;

namespace Kaleido.Process.AspNetCore.Srevices;

public interface IProcessMetadataService
{
    Task<IReadOnlyCollection<ProcessStepSummaryContract>> GetProcessesAsync(
        CancellationToken cancellationToken);
}
