using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.FunctionalTests.Fixtures;

public sealed class RegistryTestFixture
{
    public IServiceProvider ServiceProvider { get; }

    public RegistryTestFixture()
    {
        ServiceProvider = FunctionalTestServiceProviderFactory.Create(
            x => x.Namespace!.StartsWith(FunctionalTestNamespaces.Registry));
    }
}

internal static class FunctionalTestNamespaces
{
    public const string Registry =
        "Kaleido.Process.FunctionalTests.Assets.Registry";

    public const string DuplicateHandlers =
        "Kaleido.Process.FunctionalTests.Assets.DuplicateHandlers";

    public const string MissingHandlers =
        "Kaleido.Process.FunctionalTests.Assets.MissingHandlers";

    public const string Runtime =
        "Kaleido.Process.FunctionalTests.Assets.Runtime";

    public const string RuntimeFailures =
        "Kaleido.Process.FunctionalTests.Assets.RuntimeFailures";
}