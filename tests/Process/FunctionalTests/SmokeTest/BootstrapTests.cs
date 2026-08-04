//using Kaleido.Process.FunctionalTests.Fixtures;
//using Microsoft.Extensions.DependencyInjection;

//namespace Kaleido.Process.FunctionalTests.SmokeTest;

//[Collection(nameof(FunctionalTestCollection))]
//public sealed class BootstrapTests
//{
//    private readonly FunctionalTestFixture _fixture;

//    public BootstrapTests(
//        FunctionalTestFixture fixture)
//    {
//        _fixture = fixture;
//    }

//    [Fact]
//    public void ServiceProvider_ShouldBeCreated()
//    {
//        Assert.NotNull(_fixture.ServiceProvider);
//    }

//    [Fact]
//    public void CanResolveProcessStepRegistry()
//    {
//        var registry =
//            _fixture.ServiceProvider
//                .GetRequiredService<IProcessStepRegistry>();

//        Assert.NotNull(registry);
//    }
//}