using System.Net.Http.Json;
using System.Text.Json;
using Kaleido.Eventing;

namespace Kaleido.Samples.PriorAuth;

/// <summary>
/// HTTP event publisher that forwards Kaleido event envelopes to the PriorAuth EventCollector service.
/// Register via: <c>builder.Services.AddKaleido(...).AddEventPublisher&lt;HttpEventPublisher&gt;()</c>
/// The named HttpClient "PriorAuthEventCollector" must be registered separately in the host Program.cs.
/// </summary>
public sealed class HttpEventPublisher(
    IHttpClientFactory httpClientFactory)
    : IEventPublisher
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    public async Task PublishAsync<TEvent, TContext>(
        KaleidoEventEnvelope<TEvent, TContext> envelope,
        CancellationToken cancellationToken = default)
        where TEvent : IKaleidoEvent
    {
        ArgumentNullException.ThrowIfNull(envelope);

        using var response =
            await _httpClientFactory
                .CreateClient("PriorAuthEventCollector")
                .PostAsJsonAsync(
                    "/events",
                    new
                    {
                        EventType = envelope.EventType,
                        Context = (object)envelope.Context!,
                        Event = JsonSerializer.SerializeToElement(envelope.Event)
                    },
                    cancellationToken);

        response.EnsureSuccessStatusCode();
    }

}
