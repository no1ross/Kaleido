using System.Text.Json;
using Kaleido.Queryable.Eventing;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Eventing.FunctionalTests;

public sealed class EventAndLogRedactionFunctionalTests
{
    private static readonly DataClassification Phi = new("clinic", "phi");
    private static readonly DataClassification Pii = new("clinic", "pii");
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task LogsAndEvents_InOneHost_FollowTheirOwnPolicies()
    {
        await using var provider = Build(hostKeepsClassifiedValues: false, keepEventPii: true);
        var logs = provider.GetRequiredService<IRedactorProvider>();
        var patient = await WrittenPatientAsync(provider);

        Assert.Equal(string.Empty, logs.GetRedactor(new DataClassificationSet(Pii)).Redact("ava@example.com"));
        Assert.Equal("ava@example.com", patient.GetProperty("email").GetString());
        Assert.Equal(string.Empty, patient.GetProperty("diagnosis").GetString());
        Assert.Equal("Gold", patient.GetProperty("plan").GetString());
    }

    [Fact]
    public async Task Events_WithoutAnEventPolicy_AreErased_EvenWhenTheLogPolicyWouldKeepThem()
    {
        await using var provider = Build(hostKeepsClassifiedValues: true, keepEventPii: false);
        var logs = provider.GetRequiredService<IRedactorProvider>();
        var patient = await WrittenPatientAsync(provider);

        Assert.Equal("ava@example.com", logs.GetRedactor(new DataClassificationSet(Pii)).Redact("ava@example.com"));
        Assert.Equal(string.Empty, patient.GetProperty("email").GetString());
        Assert.Equal(string.Empty, patient.GetProperty("diagnosis").GetString());
    }

    private static ServiceProvider Build(bool hostKeepsClassifiedValues, bool keepEventPii)
    {
        var services = new ServiceCollection();
        services.AddRedaction(redaction =>
        {
            if (hostKeepsClassifiedValues)
            {
                redaction.SetFallbackRedactor<NullRedactor>();
            }
            else
            {
                redaction.SetRedactor<ErasingRedactor>(new DataClassificationSet(Phi), new DataClassificationSet(Pii));
            }
        });
        var builder = services.AddKaleido(new ConfigurationBuilder().Build(), new()
        {
            ServiceName = "clinic",
            ServiceVersion = "1",
            Assemblies = [typeof(EventAndLogRedactionFunctionalTests).Assembly],
            TypeFilter = type => type == typeof(PatientSource),
        });
        builder.AddEventing().AddQueryable().AddInMemoryOutboxProvider();
        if (keepEventPii)
        {
            builder.AddEventRedaction(redaction => redaction.SetRedactor<NullRedactor>(new DataClassificationSet(Pii)));
        }

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private static async Task<JsonElement> WrittenPatientAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var query = await scope.ServiceProvider.GetRequiredService<IQueryableService>()
            .QueryAsync<PatientSource, Patient>(new QueryRequest(), Cancel);
        Assert.Equal("ava@example.com", Assert.Single(query.Results).Email);
        var store = scope.ServiceProvider.GetRequiredService<IKaleidoOutboxStore>();
        var entries = await store.ClaimEventsAsync(10, TimeSpan.FromMinutes(1), Cancel);
        var entry = Assert.Single(entries, item => item.EventType == QueryableEventTypes.QueryExecuted);
        using var document = JsonDocument.Parse(entry.Payload);
        return document.RootElement.GetProperty("event").GetProperty("records")[0].Clone();
    }

    private sealed class PhiAttribute() : DataClassificationAttribute(Phi);
    private sealed class PiiAttribute() : DataClassificationAttribute(Pii);

    public sealed record Patient : IQueryContext
    {
        [Phi]
        public string Diagnosis { get; init; } = string.Empty;

        [Pii]
        public string Email { get; init; } = string.Empty;

        public string Plan { get; init; } = string.Empty;
    }

    [QuerySource(DisplayName = "Patients", Description = "Returns patient data for the event-policy test")]
    public sealed class PatientSource : IQuerySource<Patient>
    {
        public IQueryable<Patient> CreateQuery(QueryExecutionContext executionContext) =>
            new[] { new Patient { Diagnosis = "Asthma", Email = "ava@example.com", Plan = "Gold" } }.AsQueryable();
    }
}
