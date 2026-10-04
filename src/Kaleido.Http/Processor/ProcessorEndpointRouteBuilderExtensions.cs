using System.Reflection;
using Kaleido.Http.Authorization;
using Kaleido.Processor;
using Kaleido.Processor.Registry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Processor;

public static class ProcessorEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps all Kaleido Process endpoints and returns the route group so hosts can
    /// compose conventions (e.g. <c>.RequireAuthorization()</c>) onto every endpoint.
    /// </summary>
    internal static RouteGroupBuilder MapProcessor(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var registry =
            endpoints.ServiceProvider
                .GetService<IProcessorStepRegistry>();

        if (registry is null)
        {
            throw new KaleidoConfigurationException(
                ProcessorErrorCodes.InvalidRegistration,
                "Cannot map Process endpoints: Process runtime is not registered. " +
                "Use MapKaleidoHttp() to map Kaleido endpoints.");
        }

        var serviceOptions =
            endpoints.ServiceProvider
                .GetRequiredService<KaleidoServiceOptions>();

        var serviceName = serviceOptions.ServiceName;

        var logger =
            endpoints.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Kaleido.Processor.Startup");

        var group =
            endpoints.MapGroup(
                ProcessContractUrls.ProcessesPrefix(serviceName))
            .AddEndpointFilter<KaleidoJsonEndpointFilter>()
            // Post-auth: stamps CallerName/CallerRoles onto the ambient
            // correlation context for ownership checks inside handlers.
            .AddEndpointFilter<KaleidoCallerContextEndpointFilter>();

        logger.LogInformation(
            "Process endpoints mapped at route prefix {RoutePrefix} with {ProcessStepCount} process steps and {InitialStepCount} initial steps.",
            ProcessContractUrls.ProcessesPrefix(serviceName),
            registry.Registrations.Count,
            registry.InitialRegistrations.Count);

        group.MapExecuteEndpoint();

        group.MapProcessStateEndpoint();

        group.MapProcessTransferEndpoint(serviceOptions);

        foreach (var step in registry.Registrations)
        {
            if (serviceOptions.AuthorizationMode != KaleidoAuthorizationMode.ZeroTrust
                || step.Metadata.Authorization.IsExplicit())
            {
                group.MapProcessStep(step, serviceOptions);
            }
        }

        return group;
    }

    private static void MapExecuteEndpoint(
        this IEndpointRouteBuilder endpoints)
    {
        // No route-level auth: a request can carry any mix of steps, so
        // ProcessExecutionService checks every submitted step against its own
        // [KaleidoAuthorization] before anything runs and rejects the whole
        // request if any check fails.
        endpoints.MapPost(
                ProcessRoutePaths.Execute,
                async (
                    ExecuteProcessRequest request,
                    IProcessExecutionService execution,
                    CancellationToken cancellationToken) =>
                {
                    var result =
                        await execution.ExecuteAsync(
                            request,
                            cancellationToken);

                    return Results.Ok(result);
                })
            .WithName(ProcessEndpointNames.ExecuteEndpointName)
            .Accepts<ExecuteProcessRequest>("application/json")
            .WithTags("Processes")
            .Produces<ProcessExecutionResponse>()
            .WithSummary("Execute one or more process steps.")
            .WithDescription(
                "Executes one or more process steps from a single request. " +
                "This endpoint is useful when a consumer wants to submit all information currently available and let the process determine what can happen next.");
    }

    private static void MapProcessStateEndpoint(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                ProcessRoutePaths.Process,
                async (
                    Guid processId,
                    IProcessStateService stateService,
                    CancellationToken cancellationToken) =>
                {
                    var process =
                        await stateService.GetCurrentState(
                            processId,
                            cancellationToken);

                    return process is null
                        ? Results.NotFound()
                        : Results.Ok(process);
                })
            .WithName(ProcessEndpointNames.ProcessEndpointName)
            .WithTags("Processes")
            .Produces<ProcessStateResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Get processor process state.")
            .WithDescription(
                "Returns the current state of a processor process, including executed steps and currently available next steps. " +
                "This endpoint does not execute any process step.");
    }

    private static void MapProcessTransferEndpoint(
        this IEndpointRouteBuilder endpoints,
        KaleidoServiceOptions options)
    {
        endpoints.MapPost(
                ProcessRoutePaths.ProcessTransfer,
                async (
                    Guid processId,
                    IProcessStateService stateService,
                    CancellationToken cancellationToken) =>
                {
                    var transferred =
                        await stateService.TransferOwnershipAsync(
                            processId,
                            cancellationToken);

                    return transferred is null
                        ? Results.NotFound()
                        : Results.Ok(
                            new ProcessTransferResponse
                            {
                                ProcessId = processId,
                                Owner = transferred.Owner
                                    ?? throw new KaleidoFrameworkException(
                                        FrameworkErrorCodes.MissingRegistration,
                                        "Transferred process context has no owner.")
                            });
                })
            // Ownership transfer always requires an authenticated caller �
            // the handler rejects anonymous callers and enforces owner/role-mate
            // rules. Route metadata is attached only when enforcing so hosts
            // without authentication don't fail at request time.
            .RequireKaleidoAuthorization(options)
            .WithName(ProcessEndpointNames.ProcessTransferEndpointName)
            .WithTags("Processes")
            .Produces<ProcessTransferResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Transfer process ownership to the caller.")
            .WithDescription(
                "Transfers ownership of a process to the authenticated caller. " +
                "Unowned processes may be taken by any authenticated caller; " +
                "owned processes may be taken by the current owner or a caller " +
                "sharing one of the owner's roles.");
    }

    private static void MapProcessStep(
        this IEndpointRouteBuilder endpoints,
        ProcessStepRegistration step,
        KaleidoServiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(step);

        var stepName =
            step.Metadata.Name.ToLowerInvariant();

        endpoints.MapStepExecutionEndpoint(
            step,
            ProcessRoutePaths.ExecuteStep(stepName),
            options);
    }

    private static void MapStepExecutionEndpoint(
        this IEndpointRouteBuilder endpoints,
        ProcessStepRegistration step,
        string route,
        KaleidoServiceOptions options)
    {
        if (step.StepResultType is null)
        {
            var method = typeof(ProcessorEndpointRouteBuilderExtensions)
                .GetMethod(
                    nameof(MapUntypedStepExecutionEndpoint),
                    BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new KaleidoFrameworkException(
                    FrameworkErrorCodes.ReflectionError,
                    $"Method '{nameof(MapUntypedStepExecutionEndpoint)}' not found.");

            method
                .MakeGenericMethod(step.StepType)
                .Invoke(
                    null,
                    [endpoints, route, step, options]);
        }
        else
        {
            var method = typeof(ProcessorEndpointRouteBuilderExtensions)
                .GetMethod(
                    nameof(MapTypedStepExecutionEndpoint),
                    BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new KaleidoFrameworkException(
                    FrameworkErrorCodes.ReflectionError,
                    $"Method '{nameof(MapTypedStepExecutionEndpoint)}' not found.");

            method
                .MakeGenericMethod(
                    step.StepType,
                    step.StepResultType)
                .Invoke(
                    null,
                    [endpoints, route, step, options]);
        }
    }

    private static void MapTypedStepExecutionEndpoint<TProcessStep, TResponse>(
        IEndpointRouteBuilder endpoints,
        string route,
        ProcessStepRegistration step,
        KaleidoServiceOptions options)
    {
        var stepName =
            step.Metadata.Name.ToLowerInvariant();

        endpoints.MapPost(
                route,
                async (
                    ExecuteStepRequest<TProcessStep> request,
                    IProcessExecutionService execution,
                    CancellationToken cancellationToken) =>
                {
                    var result =
                        await execution.ExecuteAsync<TProcessStep, TResponse>(
                            request,
                            cancellationToken);

                    return Results.Ok(result);
                })
            .WithKaleidoAuthorization(step.Metadata.Authorization, options)
            .WithName(
                ProcessEndpointNames.StepExecutionEndpointName(
                    stepName))
            .WithTags(step.Metadata.DisplayName)
            .WithSummary(
                $"Execute {step.Metadata.DisplayName}.")
            .WithDescription(
                $"Executes the '{step.Metadata.DisplayName}' process step. " +
                "If the request does not include a processor process id, a new processor process is created. " +
                "If the request includes a processor process id, the existing processor process is continued. " +
                "The response includes the step result, consumer-facing messages, required next step if one exists, " +
                "and currently available next steps.")
            .Accepts<ExecuteStepRequest<TProcessStep>>(
                "application/json")
            .Produces<StepExecutionResponse<TResponse>>();
    }

    private static void MapUntypedStepExecutionEndpoint<TProcessStep>(
        IEndpointRouteBuilder endpoints,
        string route,
        ProcessStepRegistration step,
        KaleidoServiceOptions options)
    {
        var stepName =
            step.Metadata.Name.ToLowerInvariant();

        endpoints.MapPost(
                route,
                async (
                    ExecuteStepRequest<TProcessStep> request,
                    IProcessExecutionService execution,
                    CancellationToken cancellationToken) =>
                {
                    var result =
                        await execution.ExecuteAsync<TProcessStep>(
                            request,
                            cancellationToken);

                    return Results.Ok(result);
                })
            .WithKaleidoAuthorization(step.Metadata.Authorization, options)
            .WithName(
                ProcessEndpointNames.StepExecutionEndpointName(
                    stepName))
            .WithTags(step.Metadata.DisplayName)
            .WithSummary(
                $"Execute {step.Metadata.DisplayName}.")
            .WithDescription(
                $"Executes the '{step.Metadata.DisplayName}' process step. " +
                "If the request does not include a processor process id, a new processor process is created. " +
                "If the request includes a processor process id, the existing processor process is continued. " +
                "The response includes the step result, consumer-facing messages, required next step if one exists, " +
                "and currently available next steps.")
            .Accepts<ExecuteStepRequest<TProcessStep>>(
                "application/json")
            .Produces<StepExecutionResponse>();
    }
}
