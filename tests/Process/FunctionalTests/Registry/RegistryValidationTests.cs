using Kaleido.Process.FunctionalTests.Fixtures;
using Xunit;

namespace Kaleido.Process.FunctionalTests.Tests.Registry;

public sealed class RegistryValidationTests
{
    [Fact]
    public void AddParticipant_WhenHandlerIsMissing_Throws()
    {
        Assert.ThrowsAny<Exception>(() =>
            RegistryTestServiceProviderFactory.Create(
                FunctionalTestNamespaces.MissingHandlers));
    }

    [Fact]
    public void AddParticipant_WhenMultipleHandlersExistForSameStep_Throws()
    {
        Assert.ThrowsAny<Exception>(() =>
            RegistryTestServiceProviderFactory.Create(
                FunctionalTestNamespaces.DuplicateHandlers));
    }
}
