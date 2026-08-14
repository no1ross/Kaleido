namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;

public sealed record CategoryCatalogView
{
    public string CategoryName
    {
        get;
        init;
    }
        = string.Empty;

    public string CategoryPath
    {
        get;
        init;
    }
        = string.Empty;

    public int Level
    {
        get;
        init;
    }

    public int ProductCount
    {
        get;
        init;
    }
}
