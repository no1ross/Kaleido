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

    /// <summary>
    /// The step is irreversible or loss-causing: it cancels, deletes, withdraws or pays. Clients
    /// (and AI agents, via the MCP <c>destructiveHint</c>) treat it with extra care. Default false:
    /// the step records or changes the process state in a way the process can continue from.
    /// </summary>
    /// <remarks>
    /// A declaration: Kaleido publishes it but cannot verify it - only the implementer knows what the
    /// handler does.
    /// </remarks>
    public bool Destructive { get; init; }

    /// <summary>
    /// The step changes something outside the process state Kaleido owns - wherever that system
    /// lives: it writes to another service's data, sends an email or message, places an order,
    /// charges a payment, or calls an API that changes data. Published as the MCP
    /// <c>openWorldHint</c>. Default false.
    /// </summary>
    /// <remarks>
    /// Reading from other systems (lookups, validation) does not count, and neither does a handoff to
    /// another Kaleido processor (the same business process continuing; the result shows it as the
    /// target processor). A step can be both <see cref="Destructive"/> and external (e.g. charging a card).
    /// </remarks>
    public bool External { get; init; }

    /// <summary>
    /// The user should confirm before a client calls this step. A hint: published in the registry
    /// and in the MCP tool description ("ask the user to confirm before calling"); Kaleido does not
    /// enforce it at call time. Default false.
    /// </summary>
    public bool RequiresConfirmation { get; init; }
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

/// <summary>
/// Declares where a step input's value comes from: a field of <b>one result</b> of a query
/// (a source, or one of its views), possibly on another service. Users describe what they know
/// (a name, a date of birth); the input takes an id they cannot type.
/// </summary>
/// <remarks>
/// <para>
/// Kaleido publishes the link in the registry and never chooses the record (ADR 0001, ADR 0008):
/// a UI can render a lookup, and the MCP adapter tells an agent which query tool fills the input,
/// so it asks the user to identify the record, searches, and lets the user pick the result.
/// </para>
/// <para>
/// Inputs that share a <see cref="Group"/> are filled from the <b>same</b> result (for example a
/// member's id and enrollment id). The query is named by its published names because it may live
/// on another service; the names are checked where the registries meet (the MCP adapter reports a
/// lookup it cannot resolve), not at startup.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Lookup(Service = "member", Source = "Member", View = "MemberSearch", Field = "MemberId",
///     Display = ["DisplayName", "DateOfBirth", "MemberNumber", "PlanName"])]
/// public Guid MemberId { get; init; }
///
/// [Lookup(Service = "member", Source = "Member", View = "MemberSearch", Field = "MemberEnrollmentId")]
/// public Guid MemberEnrollmentId { get; init; }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class LookupAttribute : Attribute
{
    /// <summary>The service that publishes the query (its route prefix, as in the registry's <c>serviceName</c>).</summary>
    public required string Service { get; init; }

    /// <summary>The query source's published name (its type name).</summary>
    public required string Source { get; init; }

    /// <summary>The view of <see cref="Source"/> to query, or null to query the source directly.</summary>
    public string? View { get; init; }

    /// <summary>The result field whose value the input takes.</summary>
    public required string Field { get; init; }

    /// <summary>
    /// Inputs with the same group are filled from the same result. Defaults to one group per query
    /// (<c>{Service}/{Source}[/{View}]</c>); name groups explicitly when a step looks up the same
    /// query twice (for example a requesting and a servicing provider).
    /// </summary>
    public string? Group { get; init; }

    /// <summary>
    /// The result fields that identify a result to a person (for example name, date of birth and
    /// member number), in display order. Declaring them on any input of the group is enough.
    /// </summary>
    public string[] Display { get; init; } = [];
}
