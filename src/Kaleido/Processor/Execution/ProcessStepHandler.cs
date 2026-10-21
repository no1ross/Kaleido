namespace Kaleido.Processor.Execution;

/// <summary>
/// Executes a process step and returns a typed response alongside the outcome.
/// </summary>
/// <typeparam name="TProcessStep">The step type this handler executes.</typeparam>
/// <typeparam name="TProcessStepResult">The typed response returned to the caller.</typeparam>
/// <remarks>
/// Exactly one handler must exist per <see cref="IProcessStep"/> type; startup fails
/// when a step has no handler or more than one. Handlers run in their own DI scope
/// with the request's correlation context.
/// </remarks>
public interface IProcessStepHandler<in TProcessStep, TProcessStepResult>
    where TProcessStep : IProcessStep
{
    /// <summary>
    /// Executes <paramref name="processStep"/>.
    /// </summary>
    /// <param name="processStep">The validated step input.</param>
    /// <param name="context">The executing process and step context.</param>
    /// <param name="cancellationToken">Cancels the execution.</param>
    /// <returns>
    /// The step outcome, built with <see cref="ProcessStepHandlerResult{TProcessStepResult}"/>
    /// factory methods.
    /// </returns>
    Task<ProcessStepHandlerResult<TProcessStepResult>> ExecuteAsync(
        TProcessStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Executes a process step that returns no typed response.
/// </summary>
/// <typeparam name="TProcessStep">The step type this handler executes.</typeparam>
/// <remarks>
/// Exactly one handler must exist per <see cref="IProcessStep"/> type; startup fails
/// when a step has no handler or more than one. Handlers run in their own DI scope
/// with the request's correlation context.
/// </remarks>
public interface IProcessStepHandler<in TProcessStep>
    where TProcessStep : IProcessStep
{
    /// <summary>
    /// Executes <paramref name="processStep"/>.
    /// </summary>
    /// <param name="processStep">The validated step input.</param>
    /// <param name="context">The executing process and step context.</param>
    /// <param name="cancellationToken">Cancels the execution.</param>
    /// <returns>
    /// The step outcome, built with <see cref="ProcessStepHandlerResult"/> factory methods.
    /// </returns>
    Task<ProcessStepHandlerResult> ExecuteAsync(
        TProcessStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default);
}
