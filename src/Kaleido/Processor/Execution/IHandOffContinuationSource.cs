namespace Kaleido.Processor.Execution;

/// <summary>
/// The answer of a service a process is handed off to, as a step handler received it: the Kaleido
/// client's step and execute responses implement it, so a handler passes the response it got from the
/// target straight to <see cref="ProcessStepHandlerResult.HandOff(string, IHandOffContinuationSource)"/>.
/// </summary>
public interface IHandOffContinuationSource
{
    /// <summary>The answer in core terms, for the hand-off to <paramref name="targetProcessorName"/>.</summary>
    /// <param name="targetProcessorName">The service that answered and takes the process over.</param>
    /// <returns>The target's outcome, messages, next steps by name, questions and notice.</returns>
    HandOffContinuation ToHandOffContinuation(string targetProcessorName);
}
