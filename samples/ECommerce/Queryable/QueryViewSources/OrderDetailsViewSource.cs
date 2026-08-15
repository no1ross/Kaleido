using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;

using Kaleido.Samples.ECommerce.Data.QueryContexts;
using Kaleido.Samples.ECommerce.Data.QueryViewSources.Parameters;
using Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;

namespace Kaleido.Samples.ECommerce.Data.QueryViewSources;

[QueryView(
    Name = "order-details",
    DisplayName = "Order Details",
    Version = "1.0.0",
    Description = "Detailed order information.")]
internal sealed class OrderDetailsViewSource
    : IQueryViewSource<
        OrderQueryContext,
        OrderDetailsView,
        OrderDetailsViewParameters>
{
    public IQueryable<OrderDetailsView> CreateView(
        IQueryable<OrderQueryContext> query,
        QueryExecutionContext executionContext)
    {
        var parameters =
            executionContext
                .TryGetViewParameters<OrderDetailsViewParameters>()
            ?? new OrderDetailsViewParameters();

        if (parameters.OrderId is not null)
        {
            query =
                query.Where(x =>
                    x.OrderId ==
                    parameters.OrderId);
        }
        else
        {
            if (parameters.ParticipantProcessId is not null)
            {
                query =
                    query.Where(x =>
                        x.ParticipantProcessId ==
                        parameters.ParticipantProcessId);
            }

            if (parameters.CustomerId is not null)
            {
                query =
                    query.Where(x =>
                        x.CustomerId ==
                        parameters.CustomerId);
            }
        }

        return query
            .OrderBy(x =>
                x.ProductName)
            .Select(x =>
                new OrderDetailsView
                {
                    OrderId =
                        x.OrderId,

                    CustomerId =
                        x.CustomerId,

                    ParticipantProcessId =
                        x.ParticipantProcessId,

                    OrderNumber =
                        x.OrderNumber,

                    Status =
                        x.Status,

                    CreatedUtc =
                        x.CreatedUtc,

                    UpdatedUtc =
                        x.UpdatedUtc,

                    SubmittedUtc =
                        x.SubmittedUtc,

                    CancelledUtc =
                        x.CancelledUtc,

                    OrderItemId =
                        x.OrderItemId,

                    ProductId =
                        x.ProductId,

                    ProductName =
                        x.ProductName,

                    SupplierName =
                        x.SupplierName,

                    FamilyName =
                        x.FamilyName,

                    ModelName =
                        x.ModelName,

                    ProductSku =
                        x.ProductSku,

                    Description =
                        x.Description,

                    Quantity =
                        x.Quantity,

                    UnitPrice =
                        x.UnitPrice,

                    ExtendedPrice =
                        x.ExtendedPrice
                });
    }
}