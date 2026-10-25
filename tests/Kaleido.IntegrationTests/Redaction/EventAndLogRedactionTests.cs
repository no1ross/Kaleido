using System.Text.Json;
using Kaleido.Eventing;
using Kaleido.Queryable.Eventing;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.IntegrationTests.Redaction;

// One host, two policies (ADR 0025): logs never carry PHI or PII (the host's AddRedaction); events
// keep PII and erase PHI (AddEventRedaction). Each channel follows its own policy.
public sealed class EventAndLogRedactionTests
{
    private static readonly DataClassification Phi = new("clinic", "phi");
    private static readonly DataClassification Pii = new("clinic", "pii");

    private sealed class PhiAttribute() : DataClassificationAttribute(Phi);

    private sealed class PiiAttribute() : DataClassificationAttribute(Pii);

    private sealed record Patient
    {
        [Phi]
        public string Diagnosis { get; init; } = string.Empty;

        [Pii]
        public string Email { get; init; } = string.Empty;

        public string Plan { get; init; } = string.Empty;
    }

    private static ServiceProvider Build(Action<IKaleidoBuilder>? events = null)
    {
        var services = new ServiceCollection();
        services.AddRedaction(r => r.SetRedactor<ErasingRedactor>(new DataClassificationSet(Phi), new DataClassificationSet(Pii)));   // logs

        var kaleido = services.AddKaleido(new ConfigurationBuilder().Build(), o =>
        {
            o.ServiceName = "clinic";
            o.AuthorizationMode = KaleidoAuthorizationMode.None;
            o.Assemblies = [typeof(EventAndLogRedactionTests).Assembly];
            o.TypeFilter = type => type.Namespace == typeof(EventAndLogRedactionTests).Namespace;
        }).AddInMemoryStores();
        events?.Invoke(kaleido);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private static JsonElement WrittenPatient(ServiceProvider provider)
    {
        var envelope = new KaleidoEventEnvelope<QueryExecuted, QueryableEventContext>
        {
            EventType = KaleidoEventTypes.QueryExecuted,
            Context = new QueryableEventContext { RequestId = "req-1", ServiceName = "clinic", QuerySourceName = "Patients" },
            Event = new QueryExecuted
            {
                OccurredOn = DateTimeOffset.UtcNow,
                QuerySourceName = "Patients",
                IsDirectQuery = true,
                TotalCount = 1,
                ReturnedCount = 1,
                SortCount = 0,
                FilterProvided = false,
                Records = [new Patient { Diagnosis = "Asthma", Email = "ava@example.com", Plan = "Gold" }],
            },
        };

        var entry = provider.GetRequiredService<IKaleidoEventPayloadWriter>().Write(envelope);

        return JsonDocument.Parse(entry.Payload).RootElement.GetProperty("event").GetProperty("records")[0];
    }

    [Fact]
    public void LogsAndEvents_InOneHost_FollowTheirOwnPolicies()
    {
        using var provider = Build(kaleido => kaleido.AddEventRedaction(r => r.SetRedactor<NullRedactor>(new DataClassificationSet(Pii))));

        var logs = provider.GetRequiredService<IRedactorProvider>();
        var patient = WrittenPatient(provider);

        // Logs: PII erased by the host's policy.
        Assert.Equal(string.Empty, logs.GetRedactor(new DataClassificationSet(Pii)).Redact("ava@example.com"));

        // Events: PII kept, PHI erased (no redactor for it), unclassified untouched.
        Assert.Equal("ava@example.com", patient.GetProperty("email").GetString());
        Assert.Equal(string.Empty, patient.GetProperty("diagnosis").GetString());
        Assert.Equal("Gold", patient.GetProperty("plan").GetString());
    }

    // The host's log policy no longer applies to events: without an event policy they are erased.
    [Fact]
    public void Events_WithoutAnEventPolicy_AreErased_EvenWhenTheLogPolicyWouldKeepThem()
    {
        var services = new ServiceCollection();
        services.AddRedaction(r => r.SetFallbackRedactor<NullRedactor>());   // logs keep everything
        services.AddKaleido(new ConfigurationBuilder().Build(), o =>
        {
            o.ServiceName = "clinic";
            o.AuthorizationMode = KaleidoAuthorizationMode.None;
            o.Assemblies = [typeof(EventAndLogRedactionTests).Assembly];
            o.TypeFilter = type => type.Namespace == typeof(EventAndLogRedactionTests).Namespace;
        }).AddInMemoryStores();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        var patient = WrittenPatient(provider);

        Assert.Equal(string.Empty, patient.GetProperty("email").GetString());
        Assert.Equal(string.Empty, patient.GetProperty("diagnosis").GetString());
    }
}
