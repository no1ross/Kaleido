namespace Kaleido.Samples.ECommerce.Data.Seed;

public class TaxonomyDefinition : Dictionary<string, TaxonomyNode>
{
}

public sealed class TaxonomyNode : Dictionary<string, TaxonomyNode>
{
}

public sealed class CategoryDefinition
{
    public Dictionary<string, CategoryDefinition> Categories { get; init; }
        = [];
}

public sealed class SupplierDefinition
{
    public required string ContactName { get; init; }

    public required string Email { get; init; }

    public bool IsPreferred { get; init; }

    public Dictionary<string, ProductFamilyDefinition> Families
    {
        get;
        init;
    }
    = [];
}

public sealed class ProductFamilyDefinition
{
    public required string PrimaryCategory { get; init; }

    public List<string> RelatedCategories
    {
        get;
        init;
    }
    = [];

    public List<string> Models
    {
        get;
        init;
    }
    = [];
}






public sealed class CustomerDefinition
{
    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required string Email { get; init; }

    public string? PhoneNumber { get; init; }
}

public sealed class CatalogCategoryDefinition
{
    public int Percentage { get; init; }

    public Dictionary<string, BrandDefinition> Brands { get; init; }
        = [];
}

public sealed class BrandDefinition
{
    public Dictionary<string, ProductFamilyDefinition> Families { get; init; }
        = [];
}

internal class SeedSettings
{
    public const int ProductCount = 1500;

    public const int OrderCount = 400;

    public const int ActiveCartCount = 5;

    public const int RandomSeed = 12345;
}

