using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Parameters;

public sealed class ProductByCategoryParameters
{
    [Description("The category path used to filter products.")]
    public required string CategoryPath { get; init; }
}
