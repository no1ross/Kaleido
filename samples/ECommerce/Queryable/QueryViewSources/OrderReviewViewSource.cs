using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Kaleido.Samples.ECommerce.Data.QueryContexts;
using Kaleido.Samples.ECommerce.Data.QueryViewSources.Parameters;
using Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;

namespace Kaleido.Samples.ECommerce.Data.QueryViewSources;

[QueryView(
    Name = "order-review",
    DisplayName = "Order Review",
    Version = "1.0.0",
    Description = "Order details for review before submission.")]
internal sealed class OrderReviewViewSource
    : IQueryViewSource<
        OrderQueryContext,
        OrderReviewView,
        OrderReviewViewParameters>
{
    public IQueryable<OrderReviewView> CreateView(
        IQueryable<OrderQueryContext> query,
        QueryExecutionContext executionContext)
    {
        var parameters =
            executionContext
                .TryGetViewParameters<OrderReviewViewParameters>()
            ?? new OrderReviewViewParameters();

        if (parameters.OrderId is not null)
        {
            query =
                query.Where(x =>
                    x.OrderId ==
                    parameters.OrderId);
        }
        else
        {
            if (parameters.ProcessId is not null)
            {
                query =
                    query.Where(x =>
                        x.ProcessId ==
                        parameters.ProcessId);
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
                x.Status == OrderStatus.Started)
            .OrderBy(x =>
                x.ProductName)
            .Select(x =>
                new OrderReviewView
                {
                    OrderId =
                        x.OrderId,

                    CustomerId =
                        x.CustomerId,

                    ProcessId =
                        x.ProcessId,

                    Status =
                        x.Status,

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