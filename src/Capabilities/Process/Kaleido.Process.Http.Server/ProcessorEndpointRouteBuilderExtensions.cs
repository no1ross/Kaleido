using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kaleido.Process.Http.Server;

/// <summary>Maps Process execution and state endpoints over the locally registered runtime.</summary>
public static partial class ProcessorEndpointRouteBuilderExtensions
{
    /// <summary>Maps the versioned local Process endpoints without requiring central Registry participation.</summary>
    /// <param name="endpoints">The ASP.NET route builder.</param>
    /// <returns>The Process route group for host conventions.</returns>
    public static RouteGroupBuilder MapProcessor(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var registry = endpoints.ServiceProvider.GetService<IProcessorStepRegistry>()
            ?? throw new KaleidoConfigurationException(ProcessorErrorCodes.InvalidRegistration,
                "Cannot map Process endpoints without AddProcess().");
        if (!endpoints.ServiceProvider.GetRequiredService<IServiceProviderIsService>().IsService(typeof(IProcessIdempotencyStore)))
        {
            throw new KaleidoConfigurationException(ConfigurationErrorCodes.InvalidOptions,
                "Choose an IProcessIdempotencyStore with AddIdempotencyStore<T>() before MapProcessor().");
        }

        var options = endpoints.ServiceProvider.GetRequiredService<KaleidoServiceOptions>();
        var server = endpoints.ServiceProvider.GetRequiredService<KaleidoServerOptions>();
        var version = options.ServiceVersion ?? throw new KaleidoConfigurationException(ConfigurationErrorCodes.InvalidServiceVersion,
            "A Process HTTP service must name a version in its published routes.");
        var prefix = ProcessContractUrls.ProcessesPrefix(options.ServiceName, version);
        var group = endpoints.MapGroup(prefix)
            .AddEndpointFilter<KaleidoExceptionEndpointFilter>()
            .AddEndpointFilter<ProcessHttpExceptionEndpointFilter>()
            .AddEndpointFilter<KaleidoCorrelationEndpointFilter>()
            .AddEndpointFilter<KaleidoJsonEndpointFilter>()
            .AddEndpointFilter<KaleidoCallerContextEndpointFilter>();
        LogMapped(endpoints, prefix, registry);

        var requireCaller = !registry.Registrations.Any(step => step.Metadata.Authorization.AllowAnonymous);
        group.MapExecuteEndpoint(server, requireCaller);
        group.MapProcessStateEndpoint(server, requireCaller);

        foreach (var step in registry.Registrations.Where(x =>
                     server.AuthorizationMode != KaleidoAuthorizationMode.ZeroTrust || x.Metadata.Authorization.IsExplicit()))
        {
            var method = typeof(ProcessorEndpointRouteBuilderExtensions).GetMethod(nameof(MapStep), BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new KaleidoFrameworkException(FrameworkErrorCodes.ReflectionError, "Cannot map the Process step endpoint.");
            method.MakeGenericMethod(step.StepType).Invoke(null, [group, step, server]);
        }

        return group;
    }

    private static void MapExecuteEndpoint(this RouteGroupBuilder group, KaleidoServerOptions server, bool requireCaller)
    {
        var execute = group.MapPost(ProcessRoutePaths.Execute,
                async (KaleidoJsonBody<ExecuteProcessRequest> request, IProcessExecutionService service, CancellationToken cancellationToken) =>
                    Results.Ok(await service.ExecuteAsync(request.Value, cancellationToken)))
            .AddEndpointFilter<ProcessIdempotencyEndpointFilter>()
            .WithName(ProcessEndpointNames.ExecuteEndpointName)
            .WithTags("Processes")
            .Accepts<ExecuteProcessRequest>("application/json")
            .Produces<ProcessResult>()
            .WithSummary("Execute one or more process steps.")
            .WithDescription("Executes one or more process steps from a single request. " +
                "Submit all information currently available and let the process determine what can happen next.");
        if (requireCaller)
        {
            execute.RequireKaleidoAuthorization(server);
        }
    }

    private static void MapProcessStateEndpoint(this RouteGroupBuilder group, KaleidoServerOptions server, bool requireCaller)
    {
        var state = group.MapGet(ProcessRoutePaths.Process,
                async (string processId, IProcessStateService service, CancellationToken cancellationToken) =>
                {
                    var id = Guid.TryParse(processId, out var parsed)
                        ? parsed
                        : throw new BadHttpRequestException($"The process id '{processId}' is not a valid GUID.");
                    var process = await service.GetCurrentState(id, cancellationToken);
                    return process is null ? Results.NotFound() : Results.Ok(process);
                })
            .WithName(ProcessEndpointNames.ProcessEndpointName)
            .WithTags("Processes")
            .Produces<ProcessStateSnapshot>()
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Get processor process state.")
            .WithDescription("Returns the current state of a processor process, including recorded step statuses " +
                "and currently available next steps. This endpoint does not execute any process step.");
        if (requireCaller)
        {
            state.RequireKaleidoAuthorization(server);
        }
    }

    private static void LogMapped(IEndpointRouteBuilder endpoints, string prefix, IProcessorStepRegistry registry)
    {
        var logger = endpoints.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Kaleido.Process.Startup");
        if (logger.IsEnabled(LogLevel.Information))
        {
            LogEndpointsMapped(logger, prefix, registry.Registrations.Count, registry.InitialRegistrations.Count);
        }
    }

    private static void MapStep<TStep>(IEndpointRouteBuilder endpoints, ProcessStepRegistration step, KaleidoServerOptions options)
        where TStep : class
    {
        endpoints.MapPost(ProcessRoutePaths.ExecuteStep(step.Metadata.Name.ToLowerInvariant()),
                async (KaleidoJsonBody<ExecuteStepRequest<TStep>> request, IProcessExecutionService service, CancellationToken cancellationToken) =>
                    Results.Ok(await service.ExecuteStepAsync(request.Value, cancellationToken)))
            .AddEndpointFilter<ProcessIdempotencyEndpointFilter>()
            .WithKaleidoAuthorization(step.Metadata.Authorization, options)
            .WithName(ProcessEndpointNames.StepExecutionEndpointName(step.Metadata.Name.ToLowerInvariant()))
            .WithTags(step.Metadata.DisplayName)
            .WithSummary($"Execute {step.Metadata.DisplayName}.")
            .WithDescription($"Executes the '{step.Metadata.DisplayName}' process step. " +
                "Send a JSON body with a required processStep property containing the step input. " +
                "To resume, send X-Kaleido-Process-Id as a request header; omit it to create a new process. " +
                "The ProcessResult includes processId, state, requiredStep, availableSteps and step outcomes; " +
                "the process id is echoed in the X-Kaleido-Process-Id response header.")
            .Accepts<ExecuteStepRequest<TStep>>("application/json")
            .Produces<ProcessResult>();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Process endpoints mapped at route prefix {RoutePrefix} with {ProcessStepCount} process steps and {InitialStepCount} initial steps.")]
    private static partial void LogEndpointsMapped(ILogger logger, string routePrefix, int processStepCount, int initialStepCount);
}
