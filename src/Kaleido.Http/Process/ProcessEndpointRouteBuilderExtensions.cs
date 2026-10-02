using System.Reflection;
using Kaleido.Http.Authorization;
using Kaleido.Process.Registry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Process;

public static class ProcessEndpointRouteBuilderExtensions
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
                .GetService<IProcessStepRegistry>();

        if (registry is null)
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.ProInvalidRegistration,
                "Cannot map Process endpoints: Process runtime is not registered. " +
                "Use MapKaleidoHttp() to map Kaleido endpoints.");
        }

        var processorRegistry =
            endpoints.ServiceProvider
                .GetRequiredService<IProcessRegistry>();

        var httpOptions =
            endpoints.ServiceProvider
                .GetRequiredService<KaleidoHttpOptions>();

        var serviceOptions =
            endpoints.ServiceProvider
                .GetRequiredService<KaleidoServiceOptions>();

        var serviceName = serviceOptions.ServiceName;

        var logger =
            endpoints.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Kaleido.Process.Startup");

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

        group.MapProcessorCatalogEndpoint(processorRegistry, serviceOptions);

        group.MapExecuteEndpoint(httpOptions);

        group.MapProcessStateEndpoint();

        group.MapProcessTransferEndpoint();

        group.MapStepCatalogEndpoint(processorRegistry, serviceName);

        group.MapStepRegistryEndpoint(processorRegistry, serviceOptions);

        foreach (var step in registry.Registrations)
        {
            group.MapProcessStep(
                step,
                processorRegistry,
                serviceName,
                httpOptions);
        }

        return group;
    }

    private static void MapProcessorCatalogEndpoint(
        this IEndpointRouteBuilder endpoints,
        IProcessRegistry registry,
        KaleidoServiceOptions serviceOptions)
    {
        endpoints.MapGet(
                "",
                async (
                    HttpContext httpContext,
                    [FromServices] IProcessResponseFactory factory,
                    [FromServices] IKaleidoAuthorizer authorizer,
                    CancellationToken cancellationToken) =>
                {
                    var processors = new List<ProcessorRegistryResponse>();

                    foreach (var registration in registry.Registrations)
                    {
                        var initialSteps =
                            await authorizer.FilterAsync(registration.InitialSteps,
                                x => x.Authorization,
                                cancellationToken);

                        processors.Add(
                            factory.CreateCatalogResponse(
                                registration with
                                {
                                    InitialSteps = initialSteps
                                },
                                serviceOptions));
                    }

                    return Results.Ok(
                        new ProcessCatalogResponse
                        {
                            Processors = processors
                        });
                })
            .WithName(ProcessEndpointNames.ProcessorCatalogEndpointName)
            .WithTags("Processes")
            .Produces<ProcessCatalogResponse>()
            .WithSummary("Get process entry points.")
            .WithDescription(
                "Returns the initial process steps that can be used to start a new processor process. " +
                "This endpoint is intended to let consumers discover how a process can begin without understanding the full process graph.");
    }

    private static void MapExecuteEndpoint(
        this IEndpointRouteBuilder endpoints,
        KaleidoHttpOptions options)
    {
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
            // Multi-step requests authorize each submitted step inside
            // ProcessExecutionService � endpoint-level auth can't express
            // per-item requirements.
            .WithKaleidoAuthorization(null, options)
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
        this IEndpointRouteBuilder endpoints)
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
            // the handler additionally enforces owner/role-mate rules.
            .RequireAuthorization()
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

    private static void MapStepRegistryEndpoint(
        this IEndpointRouteBuilder endpoints,
        IProcessRegistry registry,
        KaleidoServiceOptions serviceOptions)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(serviceOptions);

        endpoints.MapGet(
                ProcessRoutePaths.StepRegistry,
                async (
                    HttpContext httpContext,
                    [FromServices] IProcessResponseFactory factory,
                    [FromServices] IKaleidoAuthorizer authorizer,
                    CancellationToken cancellationToken) =>
                {
                    var processors = new List<ProcessorRegistryResponse>();

                    foreach (var registration in registry.Registrations)
                    {
                        var steps =
                            await authorizer.FilterAsync(registration.Steps,
                                x => x.Authorization,
                                cancellationToken);

                        var initialSteps =
                            await authorizer.FilterAsync(registration.InitialSteps,
                                x => x.Authorization,
                                cancellationToken);

                        processors.Add(
                            factory.CreateRegistryResponse(
                                registration with
                                {
                                    Steps = steps,
                                    InitialSteps = initialSteps
                                },
                                serviceOptions));
                    }

                    return Results.Ok(processors);
                })
            .WithName(ProcessEndpointNames.StepRegistryEndpointName)
            .WithTags("Processes")
            .Produces<IReadOnlyCollection<ProcessorRegistryResponse>>()
            .WithSummary("Get process registry metadata.")
            .WithDescription(
                "Returns the complete process metadata registry for all registered process steps. " +
                "The response contains the information required by consumers to discover available " +
                "process capabilities, resolve execution endpoints, validate required inputs, and " +
                "initialize local process registries. This endpoint is optimized for application startup " +
                "and eliminates the need to retrieve metadata for individual process steps.");
    }

    private static void MapStepCatalogEndpoint(
        this IEndpointRouteBuilder endpoints,
        IProcessRegistry registry,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(registry);

        endpoints.MapGet(
                ProcessRoutePaths.StepCatalog,
                async (
                    HttpContext httpContext,
                    [FromServices] IProcessResponseFactory factory,
                    [FromServices] IKaleidoAuthorizer authorizer,
                    CancellationToken cancellationToken) =>
                    Results.Ok(
                        (await authorizer.FilterAsync(registry.Registrations.SelectMany(x => x.Steps),
                                x => x.Authorization,
                                cancellationToken))
                            .Select(x =>
                                factory.CreateStepSummary(
                                    new ProcessorStepSummary
                                    {
                                        Name = x.Name,
                                        Description = x.Description,
                                        DisplayName = x.DisplayName,
                                        Version = x.Version,
                                        Repeatable = x.Repeatable,
                                        Authorization = x.Authorization
                                    },
                                    serviceName))
                            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)))
            .WithName(ProcessEndpointNames.StepCatalogEndpointName)
            .WithTags("Processes")
            .Produces<IReadOnlyCollection<ProcessStepSummary>>()
            .WithSummary("Get registered process steps.")
            .WithDescription(
                "Returns a lightweight catalog of all registered process steps, including names, descriptions, repeatability, and links. " +
                "Use each step's metadata URL to retrieve fields, constraints, dependencies, and availability rules.");
    }

    private static void MapProcessStep(
        this IEndpointRouteBuilder endpoints,
        ProcessStepRegistration step,
        IProcessRegistry processorRegistry,
        string serviceName,
        KaleidoHttpOptions options)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(processorRegistry);

        var stepName =
            step.Metadata.Name.ToLowerInvariant();

        var registryStep =
            processorRegistry.Registrations
                .SelectMany(x => x.Steps)
                .Single(x => string.Equals(
                    x.Name,
                    step.Metadata.Name,
                    StringComparison.OrdinalIgnoreCase));

        endpoints.MapStepMetadataEndpoint(
            step,
            registryStep,
            ProcessRoutePaths.StepMetadata(stepName),
            serviceName,
            options);

        endpoints.MapStepExecutionEndpoint(
            step,
            ProcessRoutePaths.ExecuteStep(stepName),
            options);
    }

    private static void MapStepMetadataEndpoint(
        this IEndpointRouteBuilder endpoints,
        ProcessStepRegistration step,
        ProcessorStepRegistryItem registryStep,
        string route,
        string serviceName,
        KaleidoHttpOptions options)
    {
        endpoints.MapGet(
                route,
                ([FromServices] IProcessResponseFactory factory) => Results.Ok(
                    factory.CreateStepResponse(
                        registryStep,
                        serviceName)))
            .WithKaleidoAuthorization(step.Metadata.Authorization, options)
            .WithName(
                ProcessEndpointNames.StepMetadataEndpointName(
                    step.Metadata.Name.ToLowerInvariant()))
            .WithTags(step.Metadata.DisplayName)
            .Produces<ProcessStepResponse>()
            .WithSummary($"Get metadata for {step.Metadata.DisplayName}.")
            .WithDescription(
                $"Returns metadata describing the '{step.Metadata.DisplayName}' process step, including field definitions, " +
                "data types, validation constraints, dependency relationships, availability rules, repeatability settings, " +
                "and links required to execute or discover related process steps. " +
                "This endpoint is intended for dynamic clients such as user interfaces, workflow explorers, " +
                "and process discovery tools. This endpoint does not execute the step.");
    }

    private static void MapStepExecutionEndpoint(
        this IEndpointRouteBuilder endpoints,
        ProcessStepRegistration step,
        string route,
        KaleidoHttpOptions options)
    {
        if (step.StepResultType is null)
        {
            var method = typeof(ProcessEndpointRouteBuilderExtensions)
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
            var method = typeof(ProcessEndpointRouteBuilderExtensions)
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
        KaleidoHttpOptions options)
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
        KaleidoHttpOptions options)
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
