using Kaleido.Samples.PriorAuth.Radiology;

namespace Kaleido.Samples.PriorAuth.Radiology.Data.Entities;

public sealed class PriorAuthorization
{
    public Guid PriorAuthorizationId { get; set; }

    public Guid ProcessId { get; set; }

    public PriorAuthorizationStatus Status { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public PriorAuthorizationMember? Member { get; set; }

    public PriorAuthorizationRequestingProvider? RequestingProvider { get; set; }

    public ICollection<PriorAuthorizationRequestedService> RequestedServices { get; set; } = new List<PriorAuthorizationRequestedService>();
}
