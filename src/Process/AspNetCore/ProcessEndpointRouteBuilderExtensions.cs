using Kaleido.Process.AspNetCore.Contracts;
using Kaleido.Process.AspNetCore.Srevices;
using Kaleido.Process.Participant.Registry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
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

        var group =
            endpoints.MapGroup(options.RoutePrefix);

        group.MapParticipantCatalogEndpoint(registry, options);

        group.MapExecuteEndpoint();

        group.MapProcessStateEndpoint();

        group.MapStepCatalogEndpoint(registry, options);

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
                        new ProcessCatalogContract
                        {
                            InitialSteps = registry.InitialRegistrations
                                .OrderBy(x => x.Metadata.Name)
                                .Select(x =>
                                    ProcessStepContract.ToSummary(
                                        x,
                                        options))
                                .ToArray()
                        }))
            .WithName(ProcessEndpointNames.ParticipantCatalogEndpointName)
            .WithTags("Processes")
            .Produces<ProcessCatalogContract>();
    }

    private static void MapExecuteEndpoint(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                ProcessRoutePaths.Execute,
                async (
                    ExecuteProcessContract request,
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
            .WithTags("Processes")
            .Produces<ProcessExecutionContract>();
    }

    private static void MapProcessStateEndpoint(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                ProcessRoutePaths.Process,
                async (
                    string participantProcessId,
                    IProcessStateService stateService,
                    CancellationToken cancellationToken) =>
                {
                    var process =
                        await stateService.GetCurrentState(
                            participantProcessId,
                            cancellationToken);

                    return process is null
                        ? Results.NotFound()
                        : Results.Ok(process);
                })
            .WithName(ProcessEndpointNames.ProcessEndpointName)
            .WithTags("Processes")
            .Produces<ProcessStateContract>()
            .Produces(StatusCodes.Status404NotFound);
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
                                ProcessStepContract.ToSummary(
                                    x,
                                    options))
                            .OrderBy(x => x.Name)))
            .WithName(ProcessEndpointNames.StepCatalogEndpointName)
            .WithTags("Processes")
            .Produces<IReadOnlyCollection<ProcessStepSummaryContract>>();
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
                    ProcessStepContract.FromRegistration(
                        step,
                        options)))
            .WithName(
                ProcessEndpointNames.StepMetadataEndpointName(
                    step.Metadata.Name.ToLowerInvariant()))
            .WithTags(step.Metadata.Name)
            .Produces<ProcessStepContract>();
    }

    private static void MapStepExecutionEndpoint(
        this IEndpointRouteBuilder endpoints,
        ProcessStepRegistration step,
        string route)
    {
        var method =
            typeof(ProcessEndpointRouteBuilderExtensions)
                .GetMethod(
                    nameof(MapStepExecutionEndpointGeneric),
                    BindingFlags.NonPublic | BindingFlags.Static)!;

        method
            .MakeGenericMethod(
                step.StepType,
                step.StepResultType)
            .Invoke(
                null,
                [endpoints, route, step]);
    }

    private static void MapStepExecutionEndpointGeneric<TProcessStep, TResponse>(
        IEndpointRouteBuilder endpoints,
        string route,
        ProcessStepRegistration step)
    {
        endpoints.MapPost(
                route,
                async (
                    ExecuteStepContract<TProcessStep> request,
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
                    step.Metadata.Name.ToLowerInvariant()))
            .WithTags(step.Metadata.Name)
            .Produces<ProcessExecutionContract<TResponse>>();
    }
}
