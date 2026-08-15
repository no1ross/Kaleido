using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Kaleido.Samples.ECommerce.Data.QueryContexts;

namespace Kaleido.Samples.ECommerce.Data.QueryContexttSources;

internal sealed class CustomerQueryContextSource(
    ECommerceDbContext dbContext)
    : IQueryContextSource<CustomerQueryContext>
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