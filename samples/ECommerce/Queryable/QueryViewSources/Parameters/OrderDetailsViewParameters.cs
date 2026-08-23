using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Parameters;

public sealed record OrderDetailsViewParameters
{
    [Required]
    public Guid? ProcessId
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