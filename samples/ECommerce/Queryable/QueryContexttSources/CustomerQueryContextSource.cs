using Kaleido.Queryable;
using Kaleido.Samples.ECommerce.Data.QueryContexts;

namespace Kaleido.Samples.ECommerce.Data.QueryContexttSources;

[QuerySource(
    Version = "1.0.0",
    DisplayName = "Customers",
    Description = "Customers of the store, with their profile and contact details.",
    Source = "E-Commerce Catalog")]
internal sealed class CustomerQueryContextSource(
    ECommerceDbContext dbContext)
    : IQuerySource<CustomerQueryContext>
{
    public IQueryable<CustomerQueryContext> CreateQuery(QueryExecutionContext executionContext)
    {
        return dbContext.Customers
            .Select(customer =>
                new CustomerQueryContext
                {
                    CustomerId =
                        customer.CustomerId,

                    FirstName =
                        customer.FirstName,

                    LastName =
                        customer.LastName,

                    Email =
                        customer.Email,

                    IsActive =
                        customer.IsActive
                });
    }
}