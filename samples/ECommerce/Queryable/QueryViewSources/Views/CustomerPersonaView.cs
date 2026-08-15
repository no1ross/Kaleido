namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;

public sealed record CustomerPersonaView
{
    public Guid CustomerId
    {
        get;
        init;
    }

    public string DisplayName
    {
        get;
        init;
    } = string.Empty;

    public string Email
    {
        get;
        init;
    } = string.Empty;
}