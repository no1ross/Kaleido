namespace Kaleido.Samples.PriorAuth.History.Process;

/// <summary>
/// History did not record a step's change (an HTTP failure, or the upsert did not complete).
/// History is the source of truth for finding requests, so the step that made the change must not
/// complete: <see cref="HistoryRecording"/> rolls the step's own changes back and rejects it.
/// The client that throws it logs the failure (at Error, with History's answer) before throwing.
/// </summary>
public sealed class HistoryNotRecordedException(
    string message,
    Exception? innerException = null)
    : Exception(message, innerException);
