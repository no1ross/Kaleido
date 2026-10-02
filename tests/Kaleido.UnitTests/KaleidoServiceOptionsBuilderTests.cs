namespace Kaleido.UnitTests;

public sealed class KaleidoServiceOptionsBuilderTests : SutFixture<KaleidoServiceOptionsBuilder>
{
    protected override KaleidoServiceOptionsBuilder CreateSut() => new();

    [Fact]
    public void Build_CopiesAllPropertiesToImmutableSnapshot()
    {
        var sut = CreateSut();
        sut.ServiceName = "test-service";
        sut.DisplayName = "Test Service";
        sut.Description = "A test service";
        sut.IsEntryProcessor = true;
        sut.TypeFilter = t => true;

        var options = sut.Build();

        Assert.Equal("test-service", options.ServiceName);
        Assert.Equal("Test Service", options.DisplayName);
        Assert.Equal("A test service", options.Description);
        Assert.Equal(sut.InstanceId, options.InstanceId);
        Assert.True(options.IsEntryProcessor);
        Assert.NotNull(options.TypeFilter);
    }

    [Fact]
    public void Build_Defaults_GenerateFreshInstanceId()
    {
        var sut = CreateSut();
        sut.ServiceName = "s";

        var options = sut.Build();

        Assert.NotEqual(Guid.Empty, options.InstanceId);
        Assert.Null(options.Assemblies);
        Assert.Null(options.TypeFilter);
        Assert.False(options.IsEntryProcessor);
    }
}
