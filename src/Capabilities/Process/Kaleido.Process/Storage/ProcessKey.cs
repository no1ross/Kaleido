namespace Kaleido.Storage;

/// <summary>
/// Identifies one process's state in a store: the processor that owns it, that processor's version and
/// the process id. A hand-off keeps the process id, so the origin and the target each keep their own
/// state under it; a process of one processor version is not found by another (#552).
/// </summary>
/// <param name="ProcessorName">The owning processor: the service name (<see cref="KaleidoServiceOptions.ServiceName"/>).</param>
/// <param name="ProcessorVersion">The owning processor's version (<see cref="KaleidoServiceOptions.ServiceVersion"/>); <see langword="null"/> when it is unversioned.</param>
/// <param name="ProcessId">The process id.</param>
/// <remarks>
/// Kaleido passes the key on every load and saves each <see cref="Processor.Context.ProcessorContext"/>
/// under its own (<see cref="Processor.Context.ProcessorContext.Key"/>), so a store keeps any number of
/// processors and versions in one database: every query and write of a store must match all three parts.
/// </remarks>
[ExcludeFromCodeCoverage]
public readonly record struct ProcessKey(string ProcessorName, string? ProcessorVersion, Guid ProcessId);
