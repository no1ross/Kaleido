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

    [Fact]
    public void Build_CopiesEventsAndAuthorizationSoLaterChangesDoNotLeak()
    {
        var sut = CreateSut();
        var roles = new List<string> { "admin" };
        sut.Events.BatchSize = 7;
        sut.DefaultAuthorization = new Kaleido.Registry.AuthorizationMetadata("staff", roles);
        sut.AuthorizationMode = KaleidoAuthorizationMode.ZeroTrust;

        var options = sut.Build();
        sut.Events.BatchSize = 99;
        roles.Add("other");

        Assert.Equal(7, options.Events.BatchSize);
        Assert.NotSame(sut.Events, options.Events);
        Assert.Equal("staff", options.DefaultAuthorization.Policy);
        Assert.Equal(["admin"], options.DefaultAuthorization.Roles);
        Assert.Equal(KaleidoAuthorizationMode.ZeroTrust, options.AuthorizationMode);
    }

    [Fact]
    public void Build_WhenEventsIsNull_ThrowsInvalidEventOptions()
    {
        var sut = CreateSut();
        sut.Events = null!;

        var exception = Assert.Throws<Kaleido.Exceptions.KaleidoConfigurationException>(() => sut.Build());

        Assert.Equal(Kaleido.Exceptions.ConfigurationErrorCodes.InvalidEventOptions, exception.Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Build_WhenDefaultAuthorizationOrRolesIsNull_ThrowsConflictingAuthorization(bool nullDeclaration)
    {
        var sut = CreateSut();
        sut.DefaultAuthorization = nullDeclaration ? null! : new Kaleido.Registry.AuthorizationMetadata(null, null!);

        var exception = Assert.Throws<Kaleido.Exceptions.KaleidoConfigurationException>(() => sut.Build());

        Assert.Equal(Kaleido.Exceptions.ConfigurationErrorCodes.ConflictingAuthorization, exception.Code);
    }
}
