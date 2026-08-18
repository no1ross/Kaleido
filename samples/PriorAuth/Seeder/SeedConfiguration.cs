using Microsoft.Extensions.Configuration;

namespace Kaleido.Samples.PriorAuth.Seeder;

internal static class SeedConfiguration
{
    public static IConfiguration CreateRootConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();
    }

    public static IReadOnlyList<SupportedDomain> ResolveRequestedDomains(
        string[] args,
        IConfiguration configuration)
    {
        var overrideValue =
            args.FirstOrDefault(
                x => x.StartsWith("--domains=", StringComparison.OrdinalIgnoreCase));

        var domainValues =
            overrideValue is null
                ? configuration.GetSection("Seed:Domains")
                    .GetChildren()
                    .Select(x => x.Value)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>()
                    .ToArray()
                : overrideValue[10..].Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (domainValues.Length == 0)
        {
            return [];
        }

        return domainValues
            .Select(x => Enum.Parse<SupportedDomain>(x, ignoreCase: true))
            .Distinct()
            .ToList();
    }
}
