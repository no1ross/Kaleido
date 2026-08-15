using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Kaleido.Samples.ECommerce.Data.QueryContexts;
using Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;

namespace Kaleido.Samples.ECommerce.Data.QueryViewSources;

[QueryView(
    Name = "customer-context",
    DisplayName = "Customer Personas",
    Version = "1.0.0",
    Description = "Available customer personas.")]
internal sealed class CustomerPersonaViewSource
    : IQueryViewSource<
        CustomerQueryContext,
        CustomerPersonaView>
{
    public IQueryable<CustomerPersonaView> CreateView(
        IQueryable<CustomerQueryContext> query,
        QueryExecutionContext executionContext)
    {
        return query
            .Where(x => x.IsActive)
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .Select(x =>
                new CustomerPersonaView
                {
                    CustomerId =
                        x.CustomerId,

                    DisplayName =
                        $"{x.FirstName} {x.LastName}",

                    Email =
                        x.Email
                });
    }
}