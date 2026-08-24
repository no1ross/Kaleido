using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Kaleido.Eventing;
using Kaleido.Process.Eventing;
using Kaleido.Queryable.Eventing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Samples.PriorAuth.Common;

public static class PriorAuthEventPublisherExtensions
{
    public static IServiceCollection AddPriorAuthEventPublishing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var baseUrl =
            configuration["Services:EventCollector:BaseUrl"]
            ?? "https://localhost:8446";

        services.AddHttpClient("PriorAuthEventCollector", client =>
        {
            client.BaseAddress = new Uri(baseUrl);
        });

        services.AddSingleton<IEventPublisher, HttpEventPublisher>();

        return services;
    }
}

internal sealed class HttpEventPublisher(
    IHttpClientFactory httpClientFactory)
    : IEventPublisher
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    public async Task PublishAsync<TEvent>(
        TEvent eventData,
        CancellationToken cancellationToken = default)
        where TEvent : IKaleidoEvent
    {
        ArgumentNullException.ThrowIfNull(eventData);

        using var response =
            await _httpClientFactory
                .CreateClient("PriorAuthEventCollector")
                .PostAsJsonAsync(
                    "/events",
                    EventEnvelope.Create(eventData),
                    cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}

public sealed record EventEnvelope(
    string EventType,
    Guid? ProcessId,
    DateTimeOffset OccurredOn,
    JsonElement Payload)
{
    public static EventEnvelope Create<TEvent>(
        TEvent eventData)
        where TEvent : IKaleidoEvent
    {
        return new EventEnvelope(
            GetEventType(eventData),
            GetProcessId(eventData),
            eventData.OccurredOn,
            JsonSerializer.SerializeToElement(eventData));
    }

    private static string GetEventType<TEvent>(
        TEvent eventData)
        where TEvent : IKaleidoEvent
    {
        ArgumentNullException.ThrowIfNull(eventData);

        var attribute =
            eventData
                .GetType()
                .GetCustomAttribute<KaleidoEventAttribute>();

        if (attribute?.Type is null)
        {
            throw new InvalidOperationException(
                $"No KaleidoEventAttribute type metadata exists for '{eventData.GetType().FullName}'.");
        }

        return attribute.Type;
    }

    private static Guid? GetProcessId<TEvent>(
        TEvent eventData)
        where TEvent : IKaleidoEvent
    {
        return eventData switch
        {
            ProcessEventBase processEvent => processEvent.ProcessId,
            QueryExecuted queryEvent => queryEvent.ProcessId,
            _ => null
        };
    }
}
