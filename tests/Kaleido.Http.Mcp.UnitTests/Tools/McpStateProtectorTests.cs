using System.Buffers.Binary;
using System.Text;
using Kaleido.Http.Mcp.Observability;
using Kaleido.Observability;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Http.Mcp.Tools.UnitTests;

public sealed class McpStateProtectorTests
    : Kaleido.UnitTests.SutFixture
{
    // Long, distinctive markers: a short one (an id like "P-1") can occur in base64url text by chance (#542).
    private const string SecretId = "patient-7f3c9e2b5d1a4e68";
    private static readonly Guid ProcessId = Guid.Parse("7a1c9e40-3b2d-4f86-9c51-0e8d2a6b4f13");

    private readonly Mock<IKaleidoCorrelationContextAccessor> correlation = new();
    private readonly Mock<IKeyManager> keys = new();
    private readonly Mock<IMcpObservability> observability = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero));

    // One ephemeral key ring for the fixture: real encryption and signing, keys only in memory.
    private readonly EphemeralDataProtectionProvider provider = new();

    public McpStateProtectorTests()
    {
        CallerIs("sub-1");

        // This instance's key ring holds the key the fixture's provider protects with.
        var key = new Mock<IKey>();
        key.Setup(x => x.KeyId).Returns(KeyIdOf(provider.CreateProtector("probe").Protect("x")));
        keys.Setup(x => x.GetAllKeys()).Returns([key.Object]);
    }

    public static TheoryData<string> Kinds => [nameof(McpStateKind.Lookup), nameof(McpStateKind.Continuation), nameof(McpStateKind.Answers), nameof(McpStateKind.Confirmation)];

    // Theories take a kind by name: the enum is internal, and a theory's parameters are public.
    private static McpStateKind Kind(string name) => Enum.Parse<McpStateKind>(name);

    private void CallerIs(string? id, string? name = null) =>
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext { CallerId = id, CallerName = name });

    private McpStateProtector CreateSut(IDataProtectionProvider? dataProtection = null) =>
        new(dataProtection ?? provider, keys.Object, correlation.Object, observability.Object, time);

    private static Guid KeyIdOf(string payload)
    {
        var bytes = WebEncoders.Base64UrlDecode(payload);
        Assert.Equal(0x09F0C9F0u, BinaryPrimitives.ReadUInt32BigEndian(bytes));
        return new Guid(bytes.AsSpan(4, 16));
    }

    private void RefusedWith(McpStateKind kind, string reason, Guid? keyId = null) =>
        observability.Verify(x => x.StateRefused(McpStateProtector.TagValue(kind), reason, keyId), Times.Once);

    private static Sample State() => new("clinic_pick_patient", SecretId, ProcessId);

    // The payload of a value of the kind.
    private static string Payload(McpStateKind kind, string value) => value[McpStateProtector.Prefix(kind).Length..];

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Protect_ThenUnprotect_ForTheSameCaller_ReturnsTheState(string kindName)
    {
        var kind = Kind(kindName);
        var sut = CreateSut();

        var value = sut.Protect(kind, State());

        Assert.StartsWith(McpStateProtector.Prefix(kind), value, StringComparison.Ordinal);
        Assert.Equal(State(), sut.Unprotect<Sample>(kind, value));
        observability.Verify(x => x.StateRefused(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);
    }

    [Fact]
    public void Protect_ALookupState_RoundTripsItsCandidates()
    {
        var state = new McpLookupState(
            "clinic_pick_patient",
            "patient",
            ProcessId,
            """{"note":"x"}""",
            "Ava",
            [new McpLookupCandidate("Ava", new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["patientId"] = JsonSerializer.SerializeToElement(SecretId) })]);

        var read = CreateSut().Unprotect<McpLookupState>(McpStateKind.Lookup, CreateSut().Protect(McpStateKind.Lookup, state));

        Assert.Equal(SecretId, read?.Candidates[0].Values["patientId"].GetString());
    }

    // The client sees ciphertext: neither the ids nor the process are readable.
    [Theory]
    [MemberData(nameof(Kinds))]
    public void Protect_HidesTheIdsAndTheProcess(string kindName)
    {
        var kind = Kind(kindName);
        var value = CreateSut().Protect(kind, State());

        Assert.DoesNotContain(SecretId, value, StringComparison.Ordinal);
        Assert.DoesNotContain(ProcessId.ToString(), value, StringComparison.Ordinal);
    }

    // Each kind has its own purpose: a state of one kind is never accepted as another, even with the
    // other kind's prefix put in front of it.
    [Theory]
    [InlineData(nameof(McpStateKind.Lookup), nameof(McpStateKind.Continuation))]
    [InlineData(nameof(McpStateKind.Answers), nameof(McpStateKind.Confirmation))]
    [InlineData(nameof(McpStateKind.Confirmation), nameof(McpStateKind.Answers))]
    [InlineData(nameof(McpStateKind.Continuation), nameof(McpStateKind.Lookup))]
    public void Unprotect_AStateOfAnotherKind_IsRefusedAsInvalid(string issuedName, string expectedName)
    {
        var (issued, expected) = (Kind(issuedName), Kind(expectedName));
        var value = CreateSut().Protect(issued, State());
        var relabeled = McpStateProtector.Prefix(expected) + Payload(issued, value);

        Assert.Null(CreateSut().Unprotect<Sample>(expected, value));
        Assert.Null(CreateSut().Unprotect<Sample>(expected, relabeled));
        observability.Verify(x => x.StateRefused(McpStateProtector.TagValue(expected), McpTelemetry.StateInvalid, null), Times.Exactly(2));
    }

    // #452: a state protected on an instance whose key this one does not have - the instances do not
    // share a key ring - is refused as such, with the key's id, so the deployment problem is reported.
    [Theory]
    [MemberData(nameof(Kinds))]
    public void Unprotect_AStateFromAnInstanceWithAnotherKeyRing_IsRefusedAsAnUnknownKey(string kindName)
    {
        var kind = Kind(kindName);
        // The other instance has its own key ring (a real one, in its own folder).
        var folder = Directory.CreateTempSubdirectory("kaleido-keys-");

        try
        {
            var value = CreateSut(DataProtectionProvider.Create(folder)).Protect(kind, State());

            Assert.Null(CreateSut().Unprotect<Sample>(kind, value));
            RefusedWith(kind, McpTelemetry.StateUnknownKey, KeyIdOf(Payload(kind, value)));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData(nameof(McpStateKind.Lookup), "tampered")]
    [InlineData(nameof(McpStateKind.Continuation), "tampered")]
    [InlineData(nameof(McpStateKind.Answers), "unsigned")]
    [InlineData(nameof(McpStateKind.Confirmation), "unsigned")]
    [InlineData(nameof(McpStateKind.Confirmation), "anotherPurpose")]
    public void Unprotect_AChangedOrForeignState_IsRefusedAsInvalid(string kindName, string change)
    {
        var kind = Kind(kindName);
        var sut = CreateSut();
        var prefix = McpStateProtector.Prefix(kind);
        var payload = Payload(kind, sut.Protect(kind, State()));

        var changed = change switch
        {
            "tampered" => prefix + payload[..^4] + (payload[^4] == 'A' ? "B" : "A") + payload[^3..],
            "unsigned" => prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { state = State(), caller = "id:sub-1", expiresUtc = time.GetUtcNow().AddMinutes(5) }))),
            _ => prefix + provider.CreateProtector("another").Protect(JsonSerializer.Serialize(State())),
        };

        Assert.Null(sut.Unprotect<Sample>(kind, changed));
        RefusedWith(kind, McpTelemetry.StateInvalid);
    }

    // Another caller's state - by subject id, or by name when there is no id - is refused.
    [Theory]
    [InlineData("sub-2", null)]
    [InlineData(null, "alice")]
    [InlineData(null, null)]
    public void Unprotect_AnotherCallersState_IsRefused(string? id, string? name)
    {
        var value = CreateSut().Protect(McpStateKind.Answers, State());
        CallerIs(id, name);

        Assert.Null(CreateSut().Unprotect<Sample>(McpStateKind.Answers, value));
        RefusedWith(McpStateKind.Answers, McpTelemetry.StateAnotherCaller);
    }

    [Fact]
    public void Unprotect_ForACallerKnownByName_ReturnsTheirOwnState()
    {
        CallerIs(id: null, name: "alice");
        var value = CreateSut().Protect(McpStateKind.Confirmation, State());

        Assert.Equal(State(), CreateSut().Unprotect<Sample>(McpStateKind.Confirmation, value));
    }

    // A state past its lifetime is refused: here one protected with the real purpose and caller, already
    // expired, so only the expiry is wrong.
    [Theory]
    [MemberData(nameof(Kinds))]
    public void Unprotect_AnExpiredState_IsRefused(string kindName)
    {
        var kind = Kind(kindName);
        var expired = provider.CreateProtector(McpStateProtector.Purpose(kind))
            .Protect(JsonSerializer.Serialize(new { state = State(), caller = "id:sub-1", expiresUtc = time.GetUtcNow().AddMinutes(-1) }, KaleidoJsonOptions.Options));

        Assert.Null(CreateSut().Unprotect<Sample>(kind, McpStateProtector.Prefix(kind) + expired));
        RefusedWith(kind, McpTelemetry.StateExpired);
    }

    // A state is valid until the end of its lifetime: one tick before, it is read; at the end, refused.
    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    public void Unprotect_AroundTheEndOfTheLifetime(int ticksFromTheEnd, bool read)
    {
        var sut = CreateSut();
        var value = sut.Protect(McpStateKind.Continuation, State());

        time.Advance(McpStateProtector.Lifetime + TimeSpan.FromTicks(ticksFromTheEnd));

        Assert.Equal(read, sut.Unprotect<Sample>(McpStateKind.Continuation, value) is not null);
    }

    [Fact]
    public void Unprotect_AnOversizedState_IsRefusedBeforeItIsDecoded()
    {
        Assert.Null(CreateSut().Unprotect<Sample>(McpStateKind.Lookup, McpStateProtector.Prefix(McpStateKind.Lookup) + new string('A', McpStateProtector.MaxLength)));
        RefusedWith(McpStateKind.Lookup, McpTelemetry.StateInvalid);
        keys.Verify(x => x.GetAllKeys(), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kaleido-mcp-state:abc")]
    [InlineData("kaleido-mcp-lookup:%%%")]
    [InlineData("kaleido-mcp-lookup:bnVsbA")]
    public void Unprotect_SomethingThatIsNotALookupState_IsRefusedAsInvalid(string? value)
    {
        Assert.Null(CreateSut().Unprotect<Sample>(McpStateKind.Lookup, value));
        RefusedWith(McpStateKind.Lookup, McpTelemetry.StateInvalid);
    }

    // A protected payload whose content is not a state (JSON null, or an envelope without a state).
    [Theory]
    [InlineData("null")]
    [InlineData("""{"caller":"id:sub-1","expiresUtc":"2026-10-28T13:00:00+00:00"}""")]
    public void Unprotect_AProtectedPayloadWithoutAState_IsRefusedAsInvalid(string json)
    {
        var value = McpStateProtector.Prefix(McpStateKind.Answers) + provider.CreateProtector(McpStateProtector.Purpose(McpStateKind.Answers)).Protect(json);

        Assert.Null(CreateSut().Unprotect<Sample>(McpStateKind.Answers, value));
        RefusedWith(McpStateKind.Answers, McpTelemetry.StateInvalid);
    }

    // Protected with the right purpose, but not a state's JSON at all.
    [Fact]
    public void Unprotect_AProtectedPayloadThatIsNotJson_IsRefusedAsInvalid()
    {
        var value = McpStateProtector.Prefix(McpStateKind.Lookup) + provider.CreateProtector(McpStateProtector.Purpose(McpStateKind.Lookup)).Protect("not json");

        Assert.Null(CreateSut().Unprotect<Sample>(McpStateKind.Lookup, value));
        RefusedWith(McpStateKind.Lookup, McpTelemetry.StateInvalid);
    }

    [Fact]
    public void Protect_WithoutAState_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CreateSut().Protect<Sample>(McpStateKind.Lookup, null!));
    }

    [Fact]
    public void EachKind_HasItsOwnPrefixPurposeAndTag()
    {
        McpStateKind[] kinds = [McpStateKind.Lookup, McpStateKind.Continuation, McpStateKind.Answers, McpStateKind.Confirmation];

        Assert.Equal(4, kinds.Select(McpStateProtector.Prefix).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(4, kinds.Select(McpStateProtector.Purpose).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            [McpTelemetry.StateKindLookup, McpTelemetry.StateKindContinuation, McpTelemetry.StateKindAnswers, McpTelemetry.StateKindConfirmation],
            kinds.Select(McpStateProtector.TagValue));
    }

    // Any state shape; the protector does not look inside it.
    public sealed record Sample(string Tool, string PatientId, Guid ProcessId);
}
