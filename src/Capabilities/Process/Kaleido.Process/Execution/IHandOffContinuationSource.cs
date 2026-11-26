namespace Kaleido.Process.Execution;

/// <summary>
/// A projection of a target service's process answer for a step handler that does not use
/// <see cref="ProcessResult"/> directly. It must carry the target's service version so the
/// continuation remains pinned after hand-off.
/// </summary>
public interface IHandOffContinuationSource
{
    /// <summary>The answer in core terms, for the hand-off to <paramref name="targetProcessorName"/>.</summary>
    /// <param name="targetProcessorName">The service that answered and takes the process over.</param>
    /// <returns>The target's version, outcome, messages, next step names, questions and notice.</returns>
    HandOffContinuation ToHandOffContinuation(string targetProcessorName);
}
