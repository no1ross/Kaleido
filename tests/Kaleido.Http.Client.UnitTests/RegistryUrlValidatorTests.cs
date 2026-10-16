namespace Kaleido.Http.Client.UnitTests;

public sealed class RegistryUrlValidatorTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly Uri BaseAddress = new("http://localhost/");

    [Theory]
    [InlineData("/svc/queryable/source/query")]
    [InlineData("svc/processes/steps/mystep")]
    public void RequireValidRegistryUrl_ARelativePath_IsReturnedUnchanged(string url)
    {
        Assert.Equal(url, url.RequireValidRegistryUrl("QueryUrl", BaseAddress));
    }

    [Theory]
    [InlineData("http://localhost/svc/registry")]
    [InlineData("https://localhost/svc/registry")]
    [InlineData("http://LOCALHOST:8080/svc/registry")]
    public void RequireValidRegistryUrl_ASameHostHttpUrl_IsReturnedUnchanged(string url)
    {
        Assert.Equal(url, url.RequireValidRegistryUrl("QueryUrl", BaseAddress));
    }

    [Fact]
    public void RequireValidRegistryUrl_WithoutABaseAddress_AcceptsAnyHttpHost()
    {
        const string url = "https://remote.example/svc/registry";

        Assert.Equal(url, url.RequireValidRegistryUrl("QueryUrl", baseAddress: null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://")]
    [InlineData("http://localhost:99999/svc")]
    public void RequireValidRegistryUrl_AMissingOrMalformedUrl_Throws(string? url)
    {
        var exception = Assert.Throws<KaleidoHttpClientException>(
            () => url.RequireValidRegistryUrl("ExecuteUrl", BaseAddress));

        Assert.Equal(HttpClientErrorCodes.InvalidRegistryUrl, exception.Code);
        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Contains("'ExecuteUrl'", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ftp://localhost/svc/registry")]
    [InlineData("file://localhost/etc/passwd")]
    public void RequireValidRegistryUrl_ANonHttpScheme_Throws(string url)
    {
        var exception = Assert.Throws<KaleidoHttpClientException>(
            () => url.RequireValidRegistryUrl("QueryUrl", BaseAddress));

        Assert.Equal(HttpClientErrorCodes.InvalidRegistryUrl, exception.Code);
    }

    [Fact]
    public void RequireValidRegistryUrl_AnotherHost_Throws()
    {
        const string url = "http://evil.example/svc/registry";

        var exception = Assert.Throws<KaleidoHttpClientException>(
            () => url.RequireValidRegistryUrl("QueryUrl", BaseAddress));

        Assert.Equal(HttpClientErrorCodes.InvalidRegistryUrl, exception.Code);
        Assert.Contains(url, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RequireValidRegistryUrl_ANonHttpSchemeWithoutABaseAddress_Throws()
    {
        const string url = "ftp://remote.example/svc/registry";

        var exception = Assert.Throws<KaleidoHttpClientException>(
            () => url.RequireValidRegistryUrl("QueryUrl", baseAddress: null));

        Assert.Equal(HttpClientErrorCodes.InvalidRegistryUrl, exception.Code);
    }
}
