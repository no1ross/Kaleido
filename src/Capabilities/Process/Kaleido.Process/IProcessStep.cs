namespace Kaleido.Process;

/// <summary>
/// Marks a type as a Kaleido process step. This interface is the step's
/// <b>identity</b>: discovery finds steps by it, handlers and results are
/// constrained to it, and relationship attributes may only reference it.
/// </summary>
/// <remarks>
/// <para>
/// The step's public name — used in the registry, on the wire, in persisted
/// process state, and in events — is the type's name (<c>Type.Name</c>),
/// unmodified. Renaming the type renames the step; that is a breaking change
/// owned by the consumer.
/// </para>
/// <para>
/// Every <see cref="IProcessStep"/> type must also carry a
/// <see cref="ProcessStepAttribute"/>, which <i>describes</i> the step
/// (display name, description) but does not identify it. A step has no version: a breaking
/// change takes a new type name (ADR 0029). Startup fails
/// if the attribute is missing, or if the attribute is applied to a type that
/// does not implement this interface.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [ProcessStep(
///     DisplayName = "Capture member",
///     Description = "Captures the member for this prior authorization request.")]
/// public sealed record CaptureMemberStep : IProcessStep
/// {
///     public required string MemberId { get; init; }
/// }
/// </code>
/// </example>
public interface IProcessStep;
