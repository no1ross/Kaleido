using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.Participant;

internal sealed class ProcessStepEngine : IProcessStepEngine
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IProcessStepRegistry _registry;

    public ProcessStepEngine(
        IServiceScopeFactory scopeFactory,
        IProcessStepRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(registry);

        _scopeFactory = scopeFactory;
        _registry = registry;
    }

    public async Task<ProcessStepResult> ExecuteAsync<TProcessStep>(
        TProcessStep processStep,
        CancellationToken cancellationToken = default)
    {
        var registration =
            _registry.GetRegistration(typeof(TProcessStep));

        using var scope =
            _scopeFactory.CreateScope();

        var handler =
            scope.ServiceProvider.GetRequiredService(
                registration.HandlerType);

        return await ExecuteHandlerAsync(
            handler,
            processStep,
            cancellationToken);
    }

    private static async Task<ProcessStepResult> ExecuteHandlerAsync<TProcessStep>(
        object handler,
        TProcessStep processStep,
        CancellationToken cancellationToken)
    {
        return await ((IProcessStepHandler<TProcessStep>)handler)
            .ExecuteAsync(
                processStep,
                cancellationToken);
    }
}

