using Kaleido.Queryable;
using Kaleido.Samples.PriorAuth.Configuration.Data;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Configuration.Queryable.ContextSources;

[QuerySource(
    Version = "1.0.0",
    DisplayName = "Product Code Mappings",
    Description = "Maps procedure and service codes to the product processor that handles them.",
    Source = "Prior Authorization Configuration")]
internal sealed class ProductCodeMappingQueryContextSource(
    ConfigurationDbContext dbContext)
    : IQuerySource<ProductCodeMappingQueryContext>
{
    public IQueryable<ProductCodeMappingQueryContext> CreateQuery(
        QueryExecutionContext executionContext)
    {
        return dbContext.ProductCodeMappings
            .AsNoTracking()
            .Select(mapping =>
                new ProductCodeMappingQueryContext
                {
                    ProductCodeMappingId = mapping.ProductCodeMappingId,
                    CodeSystem = mapping.CodeSystem,
                    CodeValue = mapping.CodeValue,
                    ProcessorName = mapping.ProcessorName
                });
    }
}
