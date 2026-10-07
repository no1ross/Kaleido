namespace Kaleido.Processor;

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
/// (version, display name, description) but does not identify it. Startup fails
/// if the attribute is missing, or if the attribute is applied to a type that
/// does not implement this interface.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [ProcessStep(
///     Version = "1.0.0",
///     DisplayName = "Capture member",
///     Description = "Captures the member for this prior authorization request.")]
/// public sealed record CaptureMemberStep : IProcessStep
/// {
///     public required string MemberId { get; init; }
/// }
/// </code>
/// </example>
public interface IProcessStep;

/// <summary>
/// Required descriptive metadata for an <see cref="IProcessStep"/> type.
/// </summary>
/// <remarks>
/// This attribute describes a step; it does not identify one. The step name is
/// not declared here — it is the type's name. <see cref="DisplayName"/> and
/// <see cref="Description"/> are published through the registry and are what
/// UIs, generated documentation, and AI agents use to understand when a step
/// applies, so write them for that audience.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ProcessStepAttribute : Attribute
{
    /// <summary>
    /// The step's version, published in the registry and recorded with step
    /// history and events. Must be non-empty.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// A short, human-readable name for the step (for example
    /// <c>"Capture member"</c>). Must be non-empty.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// What the step does and when it applies, written for people and AI agents
    /// reading the registry. Must be non-empty.
    /// </summary>
    public required string Description { get; init; }
}

/// <summary>
/// Allows an <see cref="IProcessStep"/> to execute again after it has completed.
/// </summary>
/// <remarks>
/// Kaleido only knows that the step <i>may run again</i>. What a re-run means —
/// replacing previously captured data (for example, switching the member) or
/// accumulating (for example, adding another procedure) — is the handler's
/// business logic. A step without this attribute never executes again once
/// completed.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class RepeatableAttribute : Attribute
{
    /// <summary>
    /// Marks the decorated step as repeatable.
    /// </summary>
    public RepeatableAttribute()
    {
    }
}

internal interface IStepRelationshipAttribute
{
    Type StepType { get; }
}

/// <summary>
/// Declares that the decorated <see cref="IProcessStep"/> cannot execute until
/// <typeparamref name="TStep"/> has completed.
/// </summary>
/// <typeparam name="TStep">The step that must complete first.</typeparam>
/// <remarks>
/// A step may declare multiple dependencies; <i>all</i> of them must have
/// completed. Self-references and circular dependency graphs fail at startup.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TStep> : Attribute, IStepRelationshipAttribute
    where TStep : IProcessStep
{
    /// <summary>
    /// The step type this dependency refers to (<typeparamref name="TStep"/>).
    /// </summary>
    public Type StepType => typeof(TStep);
}

/// <summary>
/// Declares that the decorated <see cref="IProcessStep"/> becomes available only
/// after <typeparamref name="TStep"/> has completed.
/// </summary>
/// <typeparam name="TStep">The step whose completion makes this step available.</typeparam>
/// <remarks>
/// A step may declare multiple <c>AvailableAfter</c> relationships; <i>all</i> of
/// them must have completed. A step with no dependencies and no
/// <c>AvailableAfter</c> relationships is an initial step that can start a new
/// process. Self-references fail at startup.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class AvailableAfterAttribute<TStep> : Attribute, IStepRelationshipAttribute
    where TStep : IProcessStep
{
    /// <summary>
    /// The step type this relationship refers to (<typeparamref name="TStep"/>).
    /// </summary>
    public Type StepType => typeof(TStep);
}

/// <summary>
/// Declares that the decorated <see cref="IProcessStep"/> stops being available
/// once <typeparamref name="TStep"/> has completed.
/// </summary>
/// <typeparam name="TStep">The step whose completion closes this step's availability.</typeparam>
/// <remarks>
/// A step may declare multiple <c>AvailableUntil</c> relationships; completing
/// <i>any</i> of them closes availability. Self-references fail at startup.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class AvailableUntilAttribute<TStep> : Attribute, IStepRelationshipAttribute
    where TStep : IProcessStep
{
    /// <summary>
    /// The step type this relationship refers to (<typeparamref name="TStep"/>).
    /// </summary>
    public Type StepType => typeof(TStep);
}
