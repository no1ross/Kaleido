using Kaleido.Queryable.Query;
using Kaleido.Samples.PriorAuth.Configuration.Data;
using Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Configuration.Queryable.ContextSources;

internal sealed class ProductCodeMappingQueryContextSource(
    ConfigurationDbContext dbContext)
    : IQueryContextSource<ProductCodeMappingQueryContext>
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
