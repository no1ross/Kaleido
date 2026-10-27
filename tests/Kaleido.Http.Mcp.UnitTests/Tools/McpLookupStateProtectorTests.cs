using System.Buffers.Binary;
using System.Text;
using Kaleido.Http.Mcp.Observability;
using Kaleido.Observability;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.WebUtilities;

namespace Kaleido.Http.Mcp.Tools.UnitTests;

public sealed class McpLookupStateProtectorTests
    : Kaleido.UnitTests.SutFixture
{
    private readonly Mock<IKaleidoCorrelationContextAccessor> correlation = new();
    private readonly Mock<IKeyManager> keys = new();
    private readonly Mock<IMcpObservability> observability = new();

    // One ephemeral key ring for the fixture: real encryption and signing, keys only in memory.
    private readonly EphemeralDataProtectionProvider provider = new();

    public McpLookupStateProtectorTests()
    {
        CallerIs("sub-1");

        // This instance's key ring holds the key the fixture's provider protects with.
        var key = new Mock<IKey>();
        key.Setup(x => x.KeyId).Returns(KeyIdOf(provider.CreateProtector("probe").Protect("x")));
        keys.Setup(x => x.GetAllKeys()).Returns([key.Object]);
    }

    private void CallerIs(string? id, string? name = null) =>
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext { CallerId = id, CallerName = name });

    private McpLookupStateProtector CreateSut(IDataProtectionProvider? dataProtection = null) =>
        new(dataProtection ?? provider, keys.Object, correlation.Object, observability.Object);

    private static Guid KeyIdOf(string payload)
    {
        var bytes = WebEncoders.Base64UrlDecode(payload);
        Assert.Equal(0x09F0C9F0u, BinaryPrimitives.ReadUInt32BigEndian(bytes));
        return new Guid(bytes.AsSpan(4, 16));
    }

    private void RefusedWith(string reason, Guid? keyId = null) =>
        observability.Verify(x => x.LookupStateRefused(reason, keyId), Times.Once);

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
        Assert.InRange(state.ExpiresUtc, DateTimeOffset.UtcNow.AddMinutes(29), DateTimeOffset.UtcNow.AddMinutes(31));
        observability.Verify(x => x.LookupStateRefused(It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);
    }

    // The client sees ciphertext: neither the ids nor the process are readable.
    [Fact]
    public void Protect_HidesTheIdsAndTheProcess()
    {
        var value = CreateSut().Protect(State());

        Assert.DoesNotContain("P-1", value, StringComparison.Ordinal);
        Assert.DoesNotContain("11111111", value, StringComparison.Ordinal);
    }

    // #452: a state protected on an instance whose key this one does not have - the instances do not
    // share a key ring - is refused as such, with the key's id, so the deployment problem is reported.
    [Fact]
    public void Unprotect_AStateFromAnInstanceWithAnotherKeyRing_IsRefusedAsAnUnknownKey()
    {
        // The other instance has its own key ring (a real one, in its own folder).
        var folder = Directory.CreateTempSubdirectory("kaleido-keys-");

        try
        {
            var value = CreateSut(DataProtectionProvider.Create(folder)).Protect(State());

            Assert.Null(CreateSut().Unprotect(value));
            RefusedWith(McpTelemetry.LookupStateUnknownKey, KeyIdOf(value[McpLookupStateProtector.Prefix.Length..]));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("tampered")]
    [InlineData("unsigned")]
    [InlineData("anotherPurpose")]
    public void Unprotect_AChangedOrForeignState_IsRefusedAsInvalid(string kind)
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
        RefusedWith(McpTelemetry.LookupStateInvalid);
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
        RefusedWith(McpTelemetry.LookupStateAnotherCaller);
    }

    [Fact]
    public void Unprotect_ForACallerKnownByName_ReturnsTheirOwnState()
    {
        CallerIs(id: null, name: "alice");
        var value = CreateSut().Protect(State());

        Assert.Equal("name:alice", CreateSut().Unprotect(value)?.Caller);
    }

    // A state past its lifetime is refused: here one protected with the real purpose and caller, already
    // expired, so only the expiry is wrong.
    [Fact]
    public void Unprotect_AnExpiredState_IsRefused()
    {
        var expired = provider.CreateProtector(McpLookupStateProtector.Purpose)
            .Protect(JsonSerializer.Serialize(State() with { Caller = "id:sub-1", ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1) }, KaleidoJsonOptions.Options));

        Assert.Null(CreateSut().Unprotect(McpLookupStateProtector.Prefix + expired));
        RefusedWith(McpTelemetry.LookupStateExpired);
    }

    [Fact]
    public void Unprotect_AnOversizedState_IsRefusedBeforeItIsDecoded()
    {
        Assert.Null(CreateSut().Unprotect(McpLookupStateProtector.Prefix + new string('A', McpLookupStateProtector.MaxLength)));
        RefusedWith(McpTelemetry.LookupStateInvalid);
        keys.Verify(x => x.GetAllKeys(), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kaleido-mcp:abc")]
    [InlineData("kaleido-mcp-lookup:%%%")]
    [InlineData("kaleido-mcp-lookup:bnVsbA")]
    public void Unprotect_SomethingThatIsNotALookupState_IsRefusedAsInvalid(string? value)
    {
        Assert.Null(CreateSut().Unprotect(value));
        RefusedWith(McpTelemetry.LookupStateInvalid);
    }

    // A protected payload whose content is not a lookup state (JSON null) is refused too.
    [Fact]
    public void Unprotect_AProtectedNull_IsRefusedAsInvalid()
    {
        var value = McpLookupStateProtector.Prefix + provider.CreateProtector(McpLookupStateProtector.Purpose).Protect("null");

        Assert.Null(CreateSut().Unprotect(value));
        RefusedWith(McpTelemetry.LookupStateInvalid);
    }

    // Protected with the right purpose, but not a lookup state's JSON at all.
    [Fact]
    public void Unprotect_AProtectedPayloadThatIsNotJson_IsRefusedAsInvalid()
    {
        var value = McpLookupStateProtector.Prefix + provider.CreateProtector(McpLookupStateProtector.Purpose).Protect("not json");

        Assert.Null(CreateSut().Unprotect(value));
        RefusedWith(McpTelemetry.LookupStateInvalid);
    }

    [Fact]
    public void Protect_WithoutAState_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CreateSut().Protect(null!));
    }
}
