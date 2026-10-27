using System.Text;
using Kaleido.Observability;
using Microsoft.AspNetCore.DataProtection;

namespace Kaleido.Http.Mcp.Tools.UnitTests;

public sealed class McpLookupStateProtectorTests
    : Kaleido.UnitTests.SutFixture
{
    private readonly Mock<IKaleidoCorrelationContextAccessor> correlation = new();

    // One ephemeral key ring for the fixture: real encryption and signing, keys only in memory.
    private readonly EphemeralDataProtectionProvider provider = new();

    public McpLookupStateProtectorTests() =>
        CallerIs("sub-1");

    private void CallerIs(string? id, string? name = null) =>
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext { CallerId = id, CallerName = name });

    private McpLookupStateProtector CreateSut() => new(provider, correlation.Object);

    private static McpLookupState State(Guid? processId = null) =>
        new(
            "clinic_pick_patient",
            "patient",
            processId ?? Guid.Parse("11111111-2222-3333-4444-555555555555"),
            """{"note":"x"}""",
            "Ava",
            [new McpLookupCandidate("Ava (P-1)", new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["patientId"] = JsonSerializer.SerializeToElement("P-1") })]);

    [Fact]
    public void Protect_ThenUnprotect_ForTheSameCaller_ReturnsTheState()
    {
        var sut = CreateSut();

        var value = sut.Protect(State());
        var state = sut.Unprotect(value);

        Assert.StartsWith(McpLookupStateProtector.Prefix, value, StringComparison.Ordinal);
        Assert.NotNull(state);
        Assert.Equal("clinic_pick_patient", state.Tool);
        Assert.Equal("P-1", state.Candidates[0].Values["patientId"].GetString());
        Assert.Equal("id:sub-1", state.Caller);
    }

    // The client sees ciphertext: neither the ids nor the process are readable.
    [Fact]
    public void Protect_HidesTheIdsAndTheProcess()
    {
        var value = CreateSut().Protect(State());

        Assert.DoesNotContain("P-1", value, StringComparison.Ordinal);
        Assert.DoesNotContain("11111111", value, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("tampered")]
    [InlineData("unsigned")]
    [InlineData("anotherPurpose")]
    public void Unprotect_AChangedOrForeignState_IsRefused(string kind)
    {
        var sut = CreateSut();
        var value = sut.Protect(State());
        var payload = value[McpLookupStateProtector.Prefix.Length..];

        var changed = kind switch
        {
            "tampered" => McpLookupStateProtector.Prefix + payload[..^4] + (payload[^4] == 'A' ? "B" : "A") + payload[^3..],
            "unsigned" => McpLookupStateProtector.Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(State(Guid.NewGuid())))),
            _ => McpLookupStateProtector.Prefix + provider.CreateProtector("another").Protect(JsonSerializer.Serialize(State())),
        };

        Assert.Null(sut.Unprotect(changed));
    }

    // Another caller's state - by subject id, or by name when there is no id - is refused.
    [Theory]
    [InlineData("sub-2", null)]
    [InlineData(null, "alice")]
    [InlineData(null, null)]
    public void Unprotect_AnotherCallersState_IsRefused(string? id, string? name)
    {
        var value = CreateSut().Protect(State());
        CallerIs(id, name);

        Assert.Null(CreateSut().Unprotect(value));
    }

    [Fact]
    public void Unprotect_ForACallerKnownByName_ReturnsTheirOwnState()
    {
        CallerIs(id: null, name: "alice");
        var value = CreateSut().Protect(State());

        Assert.Equal("name:alice", CreateSut().Unprotect(value)?.Caller);
    }

    // A state past its lifetime is refused: here one issued already expired, with the real purpose and
    // the caller, so only the expiry is wrong.
    [Fact]
    public void Unprotect_AnExpiredState_IsRefused()
    {
        var expired = provider.CreateProtector(McpLookupStateProtector.Purpose).ToTimeLimitedDataProtector()
            .Protect(JsonSerializer.Serialize(State() with { Caller = "id:sub-1" }, KaleidoJsonOptions.Options), DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.Null(CreateSut().Unprotect(McpLookupStateProtector.Prefix + expired));
    }

    [Fact]
    public void Unprotect_AnOversizedState_IsRefusedBeforeItIsDecoded()
    {
        Assert.Null(CreateSut().Unprotect(McpLookupStateProtector.Prefix + new string('A', McpLookupStateProtector.MaxLength)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kaleido-mcp:abc")]
    [InlineData("kaleido-mcp-lookup:%%%")]
    public void Unprotect_SomethingThatIsNotALookupState_IsRefused(string? value)
    {
        Assert.Null(CreateSut().Unprotect(value));
    }

    [Fact]
    public void Protect_WithoutAState_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CreateSut().Protect(null!));
    }
}
