using Kaleido.Samples.ECommerce.Data.Entities;

namespace Kaleido.Samples.ECommerce.Data.Seed.Seeders;

internal sealed class SupplierSeeder
{
    public void Seed(
        ECommerceDbContext dbContext,
        IReadOnlyDictionary<string, SupplierDefinition> definitions)
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
                            definition.Key,

                        ContactName =
                            definition.Value.ContactName,

                        Email =
                            definition.Value.Email,

                        IsPreferred =
                            definition.Value.IsPreferred,

                        IsActive =
                            true
                    })
                .ToList();

        dbContext.Suppliers.AddRange(
            suppliers);

        dbContext.SaveChanges();
    }
}