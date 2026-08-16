using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Parameters;

public sealed record SubmittedOrderViewParameters
{
    [Required]
    public Guid? ParticipantProcessId
    {
        get;
        init;
    }

    [Required]
    public Guid? CustomerId
    {
        get;
        init;
    }

    public Guid? OrderId
    {
        get;
        init;
    }
}