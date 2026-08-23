using Kaleido.Process.AspNetCore.Contracts;
using Kaleido.Process.AspNetCore.Srevices;
using Kaleido.Process.Participant.Registry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace Kaleido.Process.AspNetCore;

public static class ProcessEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapParticipant(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var registry =
            endpoints.ServiceProvider
                .GetRequiredService<IProcessStepRegistry>();

        var options =
            endpoints.ServiceProvider
                .GetRequiredService<IOptions<ProcessRouteOptions>>()
                .Value;

        var logger =
            endpoints.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Kaleido.Process.Startup");

        var group =
            endpoints.MapGroup(options.RoutePrefix);

        logger.LogInformation(
            "Process endpoints mapped at route prefix {RoutePrefix} with {ProcessStepCount} process steps and {InitialStepCount} initial steps.",
            options.RoutePrefix,
            registry.Registrations.Count,
            registry.InitialRegistrations.Count);

        group.MapParticipantCatalogEndpoint(registry, options);

        group.MapExecuteEndpoint();

        group.MapProcessStateEndpoint();

        group.MapStepCatalogEndpoint(registry, options);

        group.MapStepRegistryEndpoint(registry, options);

        foreach (var step in registry.Registrations)
        {
            group.MapProcessStep(
                step,
                options);
        }

        return endpoints;
    }

    private static void MapParticipantCatalogEndpoint(
        this IEndpointRouteBuilder endpoints,
        IProcessStepRegistry registry,
        ProcessRouteOptions options)
    {
        endpoints.MapGet(
                "",
                () =>
                    Results.Ok(
                        new ProcessCatalogRequest
                        {
                            InitialSteps = registry.InitialRegistrations
                                .OrderBy(x => x.Metadata.Name)
                                .Select(x =>
                                    ProcessStepResponse.ToSummary(
                                        x,
                                        options))
                                .ToArray()
                        }))
            .WithName(ProcessEndpointNames.ParticipantCatalogEndpointName)
            .WithTags("Processes")
            .Produces<ProcessCatalogRequest>()
            .WithSummary("Get process entry points.")
            .WithDescription(
                "Returns the initial process steps that can be used to start a new participant process. " +
                "This endpoint is intended to let consumers discover how a process can begin without understanding the full process graph.");
    }

    private static void MapExecuteEndpoint(
        this IEndpointRouteBuilder endpoints)
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
            .WithSummary("Get participant process state.")
            .WithDescription(
                "Returns the current state of a participant process, including executed steps and currently available next steps. " +
                "This endpoint does not execute any process step.");
    }

    private static void MapStepRegistryEndpoint(
        this IEndpointRouteBuilder endpoints,
        IProcessStepRegistry registry,
        ProcessRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(options);

        endpoints.MapGet(
                ProcessRoutePaths.StepRegistry,
                () =>
                    Results.Ok(
                        registry.Registrations
                            .Select(x =>
                                ProcessStepResponse.FromRegistration(
                                    x,
                                    options))
                            .OrderBy(x => x.Name)))
            .WithName(ProcessEndpointNames.StepRegistryEndpointName)
            .WithTags("Processes")
            .Produces<IReadOnlyCollection<ProcessStepResponse>>()
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
        IProcessStepRegistry registry,
        ProcessRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(options);

        endpoints.MapGet(
                ProcessRoutePaths.StepCatalog,
                () =>
                    Results.Ok(
                        registry.Registrations
                            .Select(x =>
                                ProcessStepResponse.ToSummary(
                                    x,
                                    options))
                            .OrderBy(x => x.Name)))
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
        ProcessRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(options);

        var stepName =
            step.Metadata.Name.ToLowerInvariant();

        endpoints.MapStepMetadataEndpoint(
            step,
            ProcessRoutePaths.StepMetadata(stepName),
            options);

        endpoints.MapStepExecutionEndpoint(
            step,
            ProcessRoutePaths.ExecuteStep(stepName));
    }

    private static void MapStepMetadataEndpoint(
        this IEndpointRouteBuilder endpoints,
        ProcessStepRegistration step,
        string route,
        ProcessRouteOptions options)
    {
        endpoints.MapGet(
                route,
                () => Results.Ok(
                    ProcessStepResponse.FromRegistration(
                        step,
                        options)))
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
        string route)
    {
        if (step.StepResultType is null)
        {
            typeof(ProcessEndpointRouteBuilderExtensions)
                .GetMethod(
                    nameof(MapUntypedStepExecutionEndpoint),
                    BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(step.StepType)
                .Invoke(
                    null,
                    [endpoints, route, step]);
        }
        else
        {
            typeof(ProcessEndpointRouteBuilderExtensions)
                .GetMethod(
                    nameof(MapTypedStepExecutionEndpoint),
                    BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(
                    step.StepType,
                    step.StepResultType)
                .Invoke(
                    null,
                    [endpoints, route, step]);
        }
    }

    private static void MapTypedStepExecutionEndpoint<TProcessStep, TResponse>(
        IEndpointRouteBuilder endpoints,
        string route,
        ProcessStepRegistration step)
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
            .WithName(
                ProcessEndpointNames.StepExecutionEndpointName(
                    stepName))
            .WithTags(step.Metadata.DisplayName)
            .WithSummary(
                $"Execute {step.Metadata.DisplayName}.")
            .WithDescription(
                $"Executes the '{step.Metadata.DisplayName}' process step. " +
                "If the request does not include a participant process id, a new participant process is created. " +
                "If the request includes a participant process id, the existing participant process is continued. " +
                "The response includes the step result, consumer-facing messages, required next step if one exists, " +
                "and currently available next steps.")
            .Accepts<ExecuteStepRequest<TProcessStep>>(
                "application/json")
            .Produces<StepExecutionResponse<TResponse>>();
    }

    private static void MapUntypedStepExecutionEndpoint<TProcessStep>(
        IEndpointRouteBuilder endpoints,
        string route,
        ProcessStepRegistration step)
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
            .WithName(
                ProcessEndpointNames.StepExecutionEndpointName(
                    stepName))
            .WithTags(step.Metadata.DisplayName)
            .WithSummary(
                $"Execute {step.Metadata.DisplayName}.")
            .WithDescription(
                $"Executes the '{step.Metadata.DisplayName}' process step. " +
                "If the request does not include a participant process id, a new participant process is created. " +
                "If the request includes a participant process id, the existing participant process is continued. " +
                "The response includes the step result, consumer-facing messages, required next step if one exists, " +
                "and currently available next steps.")
            .Accepts<ExecuteStepRequest<TProcessStep>>(
                "application/json")
            .Produces<StepExecutionResponse>();
    }
}
