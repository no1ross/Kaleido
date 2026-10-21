namespace Kaleido.Http.Client.UnitTests;

public sealed class KaleidoCallCredentialTests
    : Kaleido.UnitTests.SutFixture<KaleidoCallCredential>
{
    protected override KaleidoCallCredential CreateSut() => KaleidoCallCredential.Header("Bearer carol");

    [Fact]
    public void Header_SendsTheValue()
    {
        Assert.Equal(KaleidoCallCredentialKind.Header, Sut.Kind);
        Assert.Equal("Bearer carol", Sut.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Header_WithoutAValue_Throws(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => KaleidoCallCredential.Header(value!));
    }

    [Fact]
    public void NoneAndConfigured_CarryNoValue()
    {
        Assert.Equal(KaleidoCallCredentialKind.None, KaleidoCallCredential.None.Kind);
        Assert.Null(KaleidoCallCredential.None.Value);
        Assert.Equal(KaleidoCallCredentialKind.Configured, KaleidoCallCredential.Configured.Kind);
        Assert.Null(KaleidoCallCredential.Configured.Value);
    }
}
