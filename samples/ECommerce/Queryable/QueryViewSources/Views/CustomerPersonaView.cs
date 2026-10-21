using System.ComponentModel;

namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;

public sealed record CustomerPersonaView
{
    [Description("The unique ID of the customer; only active customers are returned.")]
    public Guid CustomerId
    {
        get;
        init;
    }

    [Description("The customer's first and last name separated by a space.")]
    public string DisplayName
    {
        get;
        init;
    } = string.Empty;

    [Description("The customer's email address.")]
    public string Email
    {
        get;
        init;
    } = string.Empty;
}
