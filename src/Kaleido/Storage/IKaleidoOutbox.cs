namespace Kaleido.Storage;

/// <summary>
/// The outbox integration of a Kaleido service: makes Kaleido's events durable and gets them
/// published. Kaleido decides <em>when</em> an event exists and whether it is part of a process-state
/// transition; the outbox integration decides <em>how</em> it becomes durable and is delivered
/// (ADR 0023).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Three integrations, one per service.</strong>
/// </para>
/// <list type="bullet">
/// <item><description><strong>Kaleido's built-in outbox</strong> (the default): events are added to
/// an <see cref="IKaleidoOutboxStore"/> (in memory, or durable: e.g. <c>AddSqliteOutboxStore</c>, or
/// <c>AddOutboxStore&lt;T&gt;()</c>) and Kaleido's relay hands them to <see cref="IEventPublisher"/>,
/// retrying until it succeeds. It produces events only when a publisher is registered
/// (<c>AddEventPublisher&lt;T&gt;()</c>).</description></item>
/// <item><description><strong>The host's own outbox</strong> (<c>AddOutbox&lt;T&gt;()</c>): an
/// implementation of this interface writes into an outbox the host already runs (MassTransit,
/// NServiceBus, its own table and dispatcher). Kaleido's outbox store and relay are not used, and no
/// <see cref="IEventPublisher"/> is needed.</description></item>
/// <item><description><strong>Direct publication</strong> (<c>AddDirectPublication()</c>): events are
/// published after the transition commits, once, with no durability - weaker, chosen
/// explicitly.</description></item>
/// </list>
/// <para>
/// <strong>Transitions.</strong> For a process-state transition (a process created, a step completed)
/// Kaleido runs: <see cref="IKaleidoTransactions.BeginAsync"/>,
/// <see cref="IKaleidoContextStore.SaveAsync"/>, <see cref="AddAsync"/> with the transition's events,
/// <see cref="IKaleidoTransaction.CommitAsync"/>, then <see cref="CommittedAsync"/>. State and events
/// are atomic only when this outbox writes to the database the transaction covers (e.g. the same
/// <c>DbContext</c>, or a provider's transaction it joins); otherwise each write stands alone. Events
/// that are not part of a transition (plan built, execution completed, query executed) are added
/// without a transaction: <see cref="AddAsync"/>, then <see cref="CommittedAsync"/>.
/// </para>
/// <para>
/// Each entry's <see cref="KaleidoOutboxEvent.Payload"/> is the serialized, redacted event envelope;
/// consumers drop duplicates by <see cref="KaleidoOutboxEvent.EventId"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // The host's own outbox table, on the DbContext that also holds its process state:
/// public sealed class OrdersOutbox(OrdersDbContext db) : IKaleidoOutbox
/// {
///     public async Task AddAsync(IReadOnlyCollection&lt;KaleidoOutboxEvent&gt; events, CancellationToken cancellationToken = default)
///     {
///         db.OutboxMessages.AddRange(events.Select(e => new OutboxMessage(e.EventId, e.EventType, e.Payload, e.OccurredUtc)));
///         await db.SaveChangesAsync(cancellationToken); // inside the transition's transaction when there is one
///     }
/// }
///
/// builder.Services.AddKaleido(builder.Configuration, o => { /* ... */ })
///     .AddContextStore&lt;OrdersContextStore&gt;()
///     .AddOutbox&lt;OrdersOutbox&gt;()
///     .AddTransactions&lt;OrdersTransactions&gt;();
/// </code>
/// </example>
public interface IKaleidoOutbox
{
    /// <summary>
    /// Adds events to the outbox: within the current transition's transaction when Kaleido began one,
    /// otherwise on their own.
    /// </summary>
    /// <param name="events">The entries to add (never empty), each with its serialized envelope.</param>
    /// <param name="cancellationToken">
    /// Cancels the write. Kaleido passes <see cref="CancellationToken.None"/> once the work the events
    /// describe is done.
    /// </param>
    /// <remarks>
    /// A failure within a transition fails the transition (nothing is committed). A failure for events
    /// outside a transition is logged and counted by Kaleido and never fails the request.
    /// </remarks>
    Task AddAsync(IReadOnlyCollection<KaleidoOutboxEvent> events, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after the events added since the last call are committed: the outbox may deliver them
    /// now (e.g. wake its dispatcher, or publish directly). Does nothing by default.
    /// </summary>
    /// <param name="cancellationToken">Cancels the notification (Kaleido passes <see cref="CancellationToken.None"/>).</param>
    /// <remarks>A failure is logged by Kaleido and never fails the request: the state is already committed.</remarks>
    Task CommittedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
