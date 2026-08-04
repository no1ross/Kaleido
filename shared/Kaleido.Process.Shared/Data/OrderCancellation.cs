namespace Kaleido.Process.Shared.Data;

public sealed class OrderCancellation
{
    public Guid OrderCancellationId { get; set; }

    public Guid OrderId { get; set; }

    public required string CancellationNumber { get; set; }

    public required string CancellationReason { get; set; }

    public bool RefundRequested { get; set; }

    public DateTimeOffset CancelledOn { get; set; }

    public Order Order { get; set; } = null!;
}