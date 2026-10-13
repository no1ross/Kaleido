namespace Kaleido.Eventing;

/// <summary>
/// Reduces an event before it is written to the outbox, for what data classification cannot
/// express: answers to an information request (generic items), a rule per view or per caller,
/// dropping a record set. Registered with <c>AddEventRedactor&lt;TRedactor&gt;()</c>; several
/// redactors run in registration order.
/// </summary>
/// <remarks>
/// <para>
/// Event payloads are complete by design: a step's request and response, a query's records - the
/// audit trail of what was submitted and what a caller saw. Protecting sensitive values is a
/// mechanism Kaleido provides, the policy is the implementer's:
/// </para>
/// <list type="number">
/// <item><description>
/// <strong>Data classification</strong> (the common case, no code): mark properties of your view,
/// source and step types with your own <c>DataClassificationAttribute</c> subclasses (e.g.
/// <c>[PHI]</c>, <c>[PII]</c>) and choose a redactor per classification with <c>AddRedaction</c>
/// (Microsoft.Extensions.Compliance.Redaction): erase, HMAC (pseudonymize), or an own encrypting
/// redactor. Without a registered redactor, classified values are erased.
/// </description></item>
/// <item><description>
/// <strong>This hook</strong> for everything else. It runs first, on the typed envelope; data
/// classification applies afterwards, when the envelope is serialized.
/// </description></item>
/// </list>
/// <para>
/// Both run before the outbox write, so a removed or redacted value reaches neither the outbox
/// (the store's table) nor the <see cref="IEventPublisher"/>. Responses to the caller, logs and traces
/// are not affected. A redactor is a singleton, called outside any request scope; it is called only
/// when a real publisher is registered.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class AnswersRedactor : IKaleidoEventRedactor
/// {
///     public KaleidoEventEnvelope&lt;TEvent, TContext&gt; Redact&lt;TEvent, TContext&gt;(KaleidoEventEnvelope&lt;TEvent, TContext&gt; envelope)
///         where TEvent : IKaleidoEvent =>
///         envelope.Event is StepCompleted { Request: IInformationStep } step
///             ? envelope with { Event = (TEvent)(IKaleidoEvent)(step with { Request = null }) }
///             : envelope;
/// }
///
/// builder.Services.AddKaleido(builder.Configuration)
///     .AddEventPublisher&lt;MyEventPublisher&gt;()
///     .AddEventRedactor&lt;AnswersRedactor&gt;();
/// </code>
/// </example>
public interface IKaleidoEventRedactor
{
    /// <summary>
    /// Returns the envelope to write: <paramref name="envelope"/> itself, or a reduced copy (records
    /// are immutable - use <c>with</c>). Never null.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="envelope">The event as the runtime created it.</param>
    KaleidoEventEnvelope<TEvent, TContext> Redact<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope)
        where TEvent : IKaleidoEvent;
}
