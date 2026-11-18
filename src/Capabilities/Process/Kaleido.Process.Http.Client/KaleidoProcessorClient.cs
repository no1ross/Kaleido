using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Kaleido.Process.Http.Client;

internal sealed class KaleidoProcessorClient(
    string serviceName,
    string? serviceVersion,
    IKaleidoRegistryTargetResolver targets,
    IHttpClientFactory httpClientFactory,
    IKaleidoInboundCallerAccessor inboundCaller,
    IKaleidoCorrelationContextAccessor correlation,
    KaleidoServiceOptions serviceOptions)
    : IKaleidoProcessorClient
{
    internal const string ClientName = "kaleido-process";

    public async Task<ProcessStateSnapshot?> GetProcessStateAsync(Guid processId, CancellationToken cancellationToken)
    {
        var (published, route, version) = await ProcessorAsync(cancellationToken);
        var path = ProcessPublishedHttpRoute.ProcessState(OperationPath(published.Bindings, route, "state", "GET"), processId, route.GatewayRoute);
        using var request = new HttpRequestMessage(HttpMethod.Get, path.TrimStart('/'));
        Stamp(request, correlation.Current);
        using var response = await httpClientFactory.CreateClient(ClientName).SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw await response.ToClientFailureAsync($"Process state request for '{processId}' failed", cancellationToken);
        }

        var snapshot = await response.Content.ReadFromJsonAsync<ProcessStateSnapshot>(KaleidoJsonOptions.Options, cancellationToken)
            ?? throw new KaleidoHttpClientException(HttpClientErrorCodes.EmptyResponse,
                $"Process state request for '{processId}' succeeded but returned no payload.", response.StatusCode);
        RequireVersion(snapshot.ServiceVersion, version);
        return snapshot;
    }

    public async Task<ProcessResult> ExecuteAsync(ProcessRequest request, string? idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (published, route, version) = await ProcessorAsync(cancellationToken);
        var path = OperationPath(published.Bindings, route, "execute", "POST");
        var body = new ExecuteProcessRequest
        {
            Steps = [.. request.Processor.Steps.Select(step => new ProcessStepRequest
            {
                StepName = step.Key,
                Request = JsonSerializer.SerializeToElement(step.Value, KaleidoJsonOptions.Options),
            })],
        };
        return await ExecuteAsync(path, body, request.ProcessId, idempotencyKey, version, "Process execute request", cancellationToken);
    }

    public Task<ProcessResult> ExecuteStepAsync<TStep>(TStep processStep, Guid? processId, string? idempotencyKey, CancellationToken cancellationToken)
        where TStep : class
    {
        ArgumentNullException.ThrowIfNull(processStep);
        return ExecuteStepCoreAsync(typeof(TStep).Name, new ExecuteStepRequest<TStep> { ProcessStep = processStep }, processId, idempotencyKey, cancellationToken);
    }

    public Task<ProcessResult> ExecuteStepAsync(string stepName, object processStep, Guid? processId, string? idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
        ArgumentNullException.ThrowIfNull(processStep);
        return ExecuteStepCoreAsync(stepName, new ExecuteStepRequest<object> { ProcessStep = processStep }, processId, idempotencyKey, cancellationToken);
    }

    private async Task<ProcessResult> ExecuteStepCoreAsync(string stepName, object body, Guid? processId, string? idempotencyKey, CancellationToken cancellationToken)
    {
        var (published, route, version) = await ProcessorAsync(cancellationToken);
        var step = (ProcessStepContract?)published.Steps?.FirstOrDefault(x => x is not null && string.Equals(x.Name, stepName, StringComparison.OrdinalIgnoreCase))
            ?? published.InitialSteps.FirstOrDefault(x => x is not null && string.Equals(x.Name, stepName, StringComparison.OrdinalIgnoreCase))
            ?? throw new KaleidoRegistryClientException(RegistryClientErrorCodes.RouteUnavailable,
                $"The hydrated Registry does not publish step '{stepName}' on service '{serviceName}' version '{version}'.");
        var path = OperationPath(step.Bindings, route, "execute", "POST");
        return await ExecuteAsync(path, body, processId, idempotencyKey, version, $"Process step '{stepName}' request", cancellationToken);
    }

    private async Task<ProcessResult> ExecuteAsync(string path, object body, Guid? processId, string? idempotencyKey, string version, string description, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path.TrimStart('/'))
        {
            Content = JsonContent.Create(body, body.GetType(), options: KaleidoJsonOptions.Options),
        };
        var key = idempotencyKey ?? Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(key) || key.Length > 200 || key.Any(char.IsControl))
        {
            throw new ArgumentException("The logical idempotency key must be nonempty, at most 200 characters and contain no control characters.", nameof(idempotencyKey));
        }

        request.Headers.TryAddWithoutValidation(KaleidoIdempotencyHeaders.IdempotencyKey, key);
        Stamp(request, correlation.Current with { ProcessId = processId });
        using var response = await httpClientFactory.CreateClient(ClientName).SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await response.ToClientFailureAsync($"{description} failed", cancellationToken);
        }

        var result = await response.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, cancellationToken)
            ?? throw new KaleidoHttpClientException(HttpClientErrorCodes.EmptyResponse,
                $"{description} succeeded but returned no payload.", response.StatusCode);
        RequireVersion(result.ServiceVersion, version);
        return result;
    }

    private void Stamp(HttpRequestMessage request, KaleidoCorrelationContext context)
    {
        if (inboundCaller.IsInboundRequest)
        {
            request.Options.Set(KaleidoCallContext.Key, KaleidoCallContext.ForCaller(inboundCaller.Principal, inboundCaller.Credential));
        }
        else
        {
            KaleidoCallContext.MarkServiceOwn(request);
        }

        request.StampKaleidoCorrelation(context);
        if (context.ExecutingStepName.Sanitize() is { } step)
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.CallingProcessor, serviceOptions.ServiceName.Sanitize());
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.CallingStep, step);
        }
    }

    private async Task<(ProcessRegistryItem Published, RegistryTransportRoute Route, string Version)> ProcessorAsync(CancellationToken cancellationToken)
    {
        var target = await targets.ResolveAsync(serviceName, serviceVersion, ["http"], cancellationToken);
        var published = target.Registry.Processes.FirstOrDefault(x =>
            string.Equals(x.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.ServiceVersion, target.Service.ServiceVersion, StringComparison.Ordinal));
        return published is not null
            ? (published, target.Transport, target.Service.ServiceVersion)
            : throw new KaleidoRegistryClientException(RegistryClientErrorCodes.RouteUnavailable,
                $"The hydrated Registry does not publish a processor on service '{serviceName}' version '{target.Service.ServiceVersion}'.");
    }

    private static string OperationPath(RegistryTransportBindings? bindings, RegistryTransportRoute route, string operation, string method)
    {
        var paths = bindings?.Http?.Where(x => x is not null
                && string.Equals(x.Operation, operation, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Method, method, StringComparison.OrdinalIgnoreCase))
            .Take(2).ToArray() ?? [];
        return paths.Length == 1
            ? ProcessPublishedHttpRoute.RequirePath(paths[0].Path, route.GatewayRoute)
            : throw new KaleidoRegistryClientException(RegistryClientErrorCodes.RouteUnavailable,
                $"The hydrated Registry does not publish exactly one {method} {operation} operation for this Process capability.");
    }

    private static void RequireVersion(string? actual, string expected)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new KaleidoRegistryClientException(RegistryClientErrorCodes.RouteUnavailable,
                $"The Process response did not come from the selected service version '{expected}'.");
        }
    }
}
