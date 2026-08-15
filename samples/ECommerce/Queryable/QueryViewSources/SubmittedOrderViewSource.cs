using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;

using Kaleido.Samples.ECommerce.Data.QueryContexts;
using Kaleido.Samples.ECommerce.Data.QueryViewSources.Parameters;
using Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;

namespace Kaleido.Samples.ECommerce.Data.QueryViewSources;

[QueryView(
    Name = "submitted-order",
    DisplayName = "Submitted Order",
    Version = "1.0.0",
    Description = "Submitted order details.")]
internal sealed class SubmittedOrderViewSource
    : IQueryViewSource<
        OrderQueryContext,
        SubmittedOrderView,
        SubmittedOrderViewParameters>
{
    public IQueryable<SubmittedOrderView> CreateView(
        IQueryable<OrderQueryContext> query,
        QueryExecutionContext executionContext)
    {
        var parameters =
            executionContext
                .TryGetViewParameters<SubmittedOrderViewParameters>()
            ?? new SubmittedOrderViewParameters();

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
            .Where(x =>
                x.Status ==
                OrderStatus.Submitted)
            .OrderBy(x =>
                x.ProductName)
            .Select(x =>
                new SubmittedOrderView
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

                    SubmittedUtc =
                        x.SubmittedUtc,

                    OrderItemId =
                        x.OrderItemId,

                    ProductId =
                        x.ProductId,

                    ProductName =
                        x.ProductName,

                    ProductSku =
                        x.ProductSku,

                    SupplierName =
                        x.SupplierName,

                    FamilyName =
                        x.FamilyName,

                    ModelName =
                        x.ModelName,

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