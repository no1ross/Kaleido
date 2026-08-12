using Kaleido.Samples.ECommerce.Data.Entities;

namespace Kaleido.Samples.ECommerce.Data.Seed.Seeders;

internal sealed class SupplierSeeder
{
    public void Seed(
        ECommerceDbContext dbContext,
        IReadOnlyCollection<SupplierDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(
            dbContext);

        ArgumentNullException.ThrowIfNull(
            definitions);

        var suppliers =
            definitions
                .Select(
                    definition => new Supplier
                    {
                        SupplierId =
                            Guid.NewGuid(),

                        Name =
                            definition.SupplierName,

                        ContactName =
                            definition.ContactName,

                        Email =
                            definition.Email,

                        IsPreferred = false,

                        IsActive = true
                    })
                .ToList();

        dbContext.Suppliers.AddRange(
            suppliers);

        dbContext.SaveChanges();
    }
}