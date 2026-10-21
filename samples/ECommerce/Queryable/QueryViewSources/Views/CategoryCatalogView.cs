using System.ComponentModel;

namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;

public sealed record CategoryCatalogView
{
    [Description("The display name of the category, which is the last segment of its path.")]
    public string CategoryName
    {
        get;
        init;
    }
        = string.Empty;

    [Description("The full slash-separated path of the category from its top-level category, without leading or trailing slashes (for example Electronics/Laptops).")]
    public string CategoryPath
    {
        get;
        init;
    }
        = string.Empty;

    [Description("The zero-based depth of the category in the hierarchy, where 0 is a top-level category.")]
    public int Level
    {
        get;
        init;
    }

    [Description("The number of distinct matching products assigned to this category or any of its subcategories; only categories with more than one product are returned.")]
    public int ProductCount
    {
        get;
        init;
    }
}
