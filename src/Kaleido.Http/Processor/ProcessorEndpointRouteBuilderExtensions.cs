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

        // When enforcing, the generic execute and state endpoints require an
        // authenticated caller unless an anonymous caller could legitimately
        // use them, i.e. the processor has an AllowAnonymous step. Per-step
        // checks inside the handlers still apply either way.
        var requireCaller =
            !registry.Registrations.Any(step => step.Metadata.Authorization.AllowAnonymous);

        group.MapExecuteEndpoint(serviceOptions, requireCaller);

        group.MapProcessStateEndpoint(serviceOptions, requireCaller);

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
        this IEndpointRouteBuilder endpoints,
        KaleidoServiceOptions options,
        bool requireCaller)
    {
        // A request can carry any mix of steps, so ProcessExecutionService
        // checks every submitted step against its own [KaleidoAuthorization]
        // before anything runs and rejects the whole request if any check
        // fails. Route level adds only "authenticated caller" when no step
        // allows anonymous callers (and only when enforcing).
        var builder = endpoints.MapPost(
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

        if (requireCaller)
        {
            builder.RequireKaleidoAuthorization(options);
        }
    }

    private static void MapProcessStateEndpoint(
        this IEndpointRouteBuilder endpoints,
        KaleidoServiceOptions options,
        bool requireCaller)
    {
        var builder = endpoints.MapGet(
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

        if (requireCaller)
        {
            builder.RequireKaleidoAuthorization(options);
        }
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
                "Send a JSON body with a required processStep property containing the step input. " +
                "To resume, send X-Kaleido-Process-Id as a request header; omit it to create a new process. " +
                "The response includes processId, outcome, requiredStep, availableSteps, businessMessages, " +
                "and frameworkMessages (empty by default). Typed steps also include result. " +
                "The process id is echoed in the X-Kaleido-Process-Id response header.")
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
                "Send a JSON body with a required processStep property containing the step input. " +
                "To resume, send X-Kaleido-Process-Id as a request header; omit it to create a new process. " +
                "The response includes processId, outcome, requiredStep, availableSteps, businessMessages, " +
                "and frameworkMessages (empty by default). Typed steps also include result. " +
                "The process id is echoed in the X-Kaleido-Process-Id response header.")
            .Accepts<ExecuteStepRequest<TProcessStep>>(
                "application/json")
            .Produces<StepExecutionResponse>();
    }
}
