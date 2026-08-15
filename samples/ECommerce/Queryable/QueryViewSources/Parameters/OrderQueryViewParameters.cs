namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Parameters;

public sealed record OrderReviewViewParameters
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