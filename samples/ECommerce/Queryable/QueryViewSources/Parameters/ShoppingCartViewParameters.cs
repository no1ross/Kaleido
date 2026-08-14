namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Parameters;

public sealed record ShoppingCartViewParameters
{
    public Guid? ParticipantProcessId { get; set; } = null;
    public Guid? CustomerId { get; set; } = null;
}
