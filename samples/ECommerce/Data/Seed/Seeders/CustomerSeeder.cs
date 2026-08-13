using Kaleido.Samples.ECommerce.Data;
using Kaleido.Samples.ECommerce.Data.Entities;
using Kaleido.Samples.ECommerce.Data.Seed;

namespace Kaleido.Samples.ECommerce.Data.Seed.Seeders;

internal sealed class CustomerSeeder
{
    public void Seed(
        ECommerceDbContext dbContext,
        IReadOnlyCollection<CustomerDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(
            dbContext);

        ArgumentNullException.ThrowIfNull(
            definitions);

        var customers =
            definitions
                .Select(
                    definition => new Customer
                    {
                        CustomerId =
                            Guid.NewGuid(),

                        FirstName =
                            definition.FirstName,

                        LastName =
                            definition.LastName,

                        Email =
                            definition.Email,

                        PhoneNumber =
                            definition.PhoneNumber,

                        IsActive =
                            true,

                        CreatedUtc =
                            DateTime.UtcNow
                    })
                .ToList();

        dbContext.Customers.AddRange(
            customers);

        dbContext.SaveChanges();
    }
}