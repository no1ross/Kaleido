namespace Kaleido.Http.Mcp.Tools.UnitTests;

public sealed class ToolNameBuilderTests
    : Kaleido.UnitTests.SutFixture
{
    private static ToolNameBuilder CreateSut() => new();

    [Theory]
    [InlineData("radiology", "CaptureRequestingProviderStep", "radiology_capture_requesting_provider")]
    [InlineData("member", "MemberSearchViewSource", "member_member_search")]
    [InlineData("intake", "PriorAuthQueryContextSource", "intake_prior_auth")]
    [InlineData("shop", "ProductByCategoryQueryViewSource", "shop_product_by_category")]
    [InlineData("codeset", "ProcedureSource", "codeset_procedure")]
    [InlineData("radiology", "ConfirmCtInsteadOfMriStep", "radiology_confirm_ct_instead_of_mri")]
    [InlineData("radiology", "MRIInfoStep", "radiology_mri_info")]
    [InlineData("intake", "Step", "intake_step")]
    public void Build_SnakeCasesTheServiceAndTheTypeWithoutItsSuffix(string service, string type, string expected)
    {
        var name = CreateSut().Build(service, type);

        Assert.Equal(expected, name.Value);
        Assert.False(name.Truncated);
    }

    [Fact]
    public void Build_WithoutAService_UsesTheCapabilityAlone()
    {
        Assert.Equal("capture_member", CreateSut().Build(string.Empty, "CaptureMemberStep").Value);
    }

    [Fact]
    public void Build_ReplacesCharactersOutsideTheAllowedSet()
    {
        Assert.Equal("my_svc_capture2_member", CreateSut().Build("my.svc", "Capture2MemberStep").Value);
    }

    [Fact]
    public void Build_OverTheMcpLimit_IsCutToSixtyFourWithAStableHash_AndMarkedTruncated()
    {
        var type = "CaptureTheRequestingProviderAndTheirOfficeAndTheirNetworkAndEverythingElseStep";

        var first = CreateSut().Build("radiology", type);
        var second = CreateSut().Build("radiology", type);

        Assert.True(first.Truncated);
        Assert.Equal(ToolNameBuilder.MaxLength, first.Value.Length);
        Assert.Equal(first.Value, second.Value);
        Assert.Matches("^[a-z0-9_-]{1,64}$", first.Value);
    }

    [Fact]
    public void Build_RejectsAnEmptyTypeName()
    {
        Assert.Throws<ArgumentException>(() => CreateSut().Build("svc", " "));
    }
}
