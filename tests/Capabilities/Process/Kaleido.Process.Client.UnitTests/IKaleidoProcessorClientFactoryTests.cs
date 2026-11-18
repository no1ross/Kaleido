namespace Kaleido.Process.Client.UnitTests;

public sealed class IKaleidoProcessorClientFactoryTests : Kaleido.UnitTests.SutFixture
{
    [Fact]
    public void Factory_NamesCurrentAndExplicitlyPinnedServiceVersions()
    {
        var overloads = typeof(IKaleidoProcessorClientFactory).GetMethods()
            .Where(x => x.Name == nameof(IKaleidoProcessorClientFactory.GetClient)).ToArray();

        Assert.Equal(2, overloads.Length);
        Assert.All(overloads, method => Assert.Equal(typeof(IKaleidoProcessorClient), method.ReturnType));
        Assert.Contains(overloads, method => method.GetParameters().Select(x => x.Name).SequenceEqual(["serviceName"]));
        Assert.Contains(overloads, method => method.GetParameters().Select(x => x.Name).SequenceEqual(["serviceName", "serviceVersion"]));
    }
}
