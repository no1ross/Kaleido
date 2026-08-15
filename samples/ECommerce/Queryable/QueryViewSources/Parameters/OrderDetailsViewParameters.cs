namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Parameters;

public sealed record OrderDetailsViewParameters
{
    public Guid? ParticipantProcessId
    {
        get;
        init;
    }

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