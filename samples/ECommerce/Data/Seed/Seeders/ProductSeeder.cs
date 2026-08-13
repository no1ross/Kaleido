using Kaleido.Samples.ECommerce.Data;
using Kaleido.Samples.ECommerce.Data.Entities;
using Kaleido.Samples.ECommerce.Data.Seed;

namespace Kaleido.Samples.ECommerce.Data.Seed.Seeders;

internal sealed class ProductSeeder
{
    public void Seed(
        ECommerceDbContext dbContext,
        IReadOnlyCollection<SupplierDefinition> suppliers)
    {
        ArgumentNullException.ThrowIfNull(
            dbContext);

        ArgumentNullException.ThrowIfNull(
            suppliers);

        var supplierLookup =
            dbContext.Suppliers
                .ToDictionary(
                    x => x.Name,
                    StringComparer.OrdinalIgnoreCase);

        var products =
            new List<Product>();

        foreach (var supplierDefinition in suppliers)
        {
            var supplierName =
                supplierDefinition.SupplierName;

            var supplier =
                supplierLookup[supplierName];

            //generate random number for family genreation
            var numberOfFamilies = Random.Shared.Next(8, 20);

            var familyNames = FamilyNames
                .OrderBy(_ => Random.Shared.Next())
                .Take(numberOfFamilies)
                .ToArray();

            foreach (var familyName in familyNames)
            {
                var numberOfModels = Random.Shared.Next(2, 25);
                for (var model = 1; model <= numberOfModels; model++)
                {
                    var modelName = $"Model {model}";
                    products.Add(
                        new Product
                        {
                            ProductId =
                                Guid.NewGuid(),
                            SupplierId =
                                supplier.SupplierId,
                            Name =
                                $"{supplierName} {familyName} {modelName}",

                            FamilyName = familyName,

                            ModelName = modelName,

                            Sku =
                                CreateSku(
                                    supplierName,
                                    familyName,
                                    modelName),
                            Description =
                                CreateDescription(
                                    supplierName,
                                    familyName,
                                    modelName),
                            Price =
                                GeneratePrice(),
                            Rating =
                                GenerateRating(),
                            ReviewCount =
                                GenerateReviewCount(),
                            CreatedUtc =
                                GenerateCreatedDate(),
                            ReleasedUtc =
                                GenerateReleaseDate(),
                            IsActive =
                                GenerateIsActive()
                        });
                }
            }
        }

        dbContext.Products.AddRange(
            products);

        dbContext.SaveChanges();
    }

    private static string CreateSku(
        string supplier,
        string family,
        string model)
    {
        var supplierCode =
            supplier[..Math.Min(4, supplier.Length)]
                .ToUpperInvariant();

        var familyCode =
            family[..Math.Min(4, family.Length)]
                .ToUpperInvariant();

        var modelNumber =
            model.Replace("Model ", "")
                 .PadLeft(3, '0');

        return
            $"{supplierCode}-{familyCode}-{modelNumber}-{Random.Shared.Next(100, 999)}";
    }

    private static string CreateDescription(
        string supplier,
        string family,
        string model)
    {
        return
            $"{supplier} {family} {model} is a product offered by {supplier}.";
    }

    private static decimal GeneratePrice()
    {
        var price =
            Math.Pow(
                Random.Shared.NextDouble(),
                2);

        return Math.Round(
            (decimal)(
                10 + price * 4990),
            2);
    }

    private static double GenerateRating()
        => Math.Round(
            Random.Shared.NextDouble() * 2 + 3,
            1);

    private static int GenerateReviewCount()
        => Random.Shared.Next(
            0,
            5000);

    private static DateTime GenerateCreatedDate()
        => DateTime.UtcNow.AddDays(
            -Random.Shared.Next(
                0,
                3650));

    private static DateTime GenerateReleaseDate()
        => DateTime.UtcNow.AddDays(
            -Random.Shared.Next(
                0,
                3650));

    private static bool GenerateIsActive() =>
        Random.Shared.Next(1, 101) > 10;

    private static readonly string[] FamilyNames =
[
    "Alpha",
    "Apex",
    "Arc",
    "Arrow",
    "Aspire",
    "Atlas",
    "Aura",
    "Axis",

    "Beacon",
    "Bolt",
    "Bridge",
    "Burst",

    "Catalyst",
    "Cedar",
    "Centric",
    "Champion",
    "Circuit",
    "Cloud",
    "Compass",
    "Core",
    "Crest",
    "Crown",

    "Delta",
    "Drift",
    "Drive",
    "Dynamic",

    "Edge",
    "Elevate",
    "Element",
    "Elite",
    "Ember",
    "Endurance",
    "Epic",
    "Essential",
    "Everest",
    "Evolution",

    "Falcon",
    "Flex",
    "Flow",
    "Flux",
    "Focus",
    "Forge",
    "Frontier",
    "Fusion",

    "Galaxy",
    "Genesis",
    "Glide",
    "Gravity",
    "Grid",
    "Growth",
    "Guardian",

    "Halo",
    "Harbor",
    "Horizon",

    "Ignite",
    "Impact",
    "Impulse",
    "Infinity",
    "Insight",

    "Journey",

    "Keystone",

    "Legacy",
    "Lift",
    "Limitless",
    "Link",
    "Logic",
    "Lumina",

    "Max",
    "Meridian",
    "Momentum",
    "Motion",

    "Nexus",
    "Nimbus",
    "Nova",

    "Omega",
    "Orbit",
    "Origin",
    "Outlook",

    "Pace",
    "Pathfinder",
    "Peak",
    "Pioneer",
    "Pivot",
    "Plus",
    "Power",
    "Precision",
    "Prime",
    "Pulse",

    "Quantum",
    "Quest",

    "Radiant",
    "Rapid",
    "Reach",
    "Reflex",
    "Rise",
    "Rocket",

    "Sage",
    "Scale",
    "Select",
    "Sentinel",
    "Shift",
    "Signal",
    "Skyline",
    "Smart",
    "Spark",
    "Spectrum",
    "Sprint",
    "Sterling",
    "Stream",
    "Strive",
    "Summit",
    "Swift",
    "Synergy",

    "Titan",
    "Torque",
    "Trailblazer",

    "Ultra",
    "Unity",

    "Vector",
    "Velocity",
    "Vertex",
    "Vision",
    "Vista",
    "Volt",
    "Voyager",

    "Wave",
    "Waypoint",

    "Zen",
    "Zenith",

    "Series A",
    "Series B",
    "Series C",
    "Series D",
    "Series E",
    "Series F",
    "Series X",
    "Series Y",
    "Series Z",

    "Platform",
    "Enterprise",
    "Professional",
    "Business",
    "Advanced",
    "Standard",
    "Compact",
    "Performance",
    "Expedition",
    "Explorer",

    "North",
    "South",
    "East",
    "West",

    "Red",
    "Blue",
    "Green",
    "Silver",
    "Gold",
    "Platinum",

    "One",
    "Two",
    "Three",
    "Four",
    "Five",

    "Connect",
    "Collaborate",
    "Insight",
    "Accelerate",
    "Innovate",
    "Optimize",
    "Engage",
    "Transform",

    "Navigator",
    "Operator",
    "Director",
    "Manager",
    "Architect",
    "Builder",
    "Creator",
    "Designer",

    "Air",
    "Land",
    "Sea",
    "Space",

    "Aurora",
    "Borealis",
    "Cascade",
    "Cobalt",
    "Crimson",
    "Echo",
    "Emerald",
    "Granite",
    "Iron",
    "Jade",
    "Mariner",
    "Onyx",
    "Quartz",
    "Ruby",
    "Sapphire",
    "Slate",
    "Topaz"
];
}