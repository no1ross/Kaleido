using Kaleido.Queryable;
using System.ComponentModel;

namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Parameters;

public sealed class ProductByCategoryParameters : IQueryParameters
{
    [Description("The category path used to filter products.")]
    public required string CategoryPath { get; init; }
}
