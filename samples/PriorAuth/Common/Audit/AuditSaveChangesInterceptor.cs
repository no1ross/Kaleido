using Kaleido.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Kaleido.Samples.PriorAuth.Audit;

/// <summary>
/// Stamps the audit fields of every <see cref="IAuditable"/> row a save adds or changes, from the
/// request's caller (Kaleido's correlation context). Registered per request scope by
/// <see cref="PriorAuthAuditing.AddPriorAuthAuditing"/>.
/// </summary>
/// <remarks>
/// An added row gets the same Created and Updated values. A changed row gets new Updated values and
/// keeps its Created values even if code assigned them. A row EF sees as unchanged keeps all of them,
/// so saving the same values again does not count as a change.
/// </remarks>
public sealed class AuditSaveChangesInterceptor(
    IKaleidoCorrelationContextAccessor correlation) : SaveChangesInterceptor
{
    /// <summary>The caller recorded when the request has none (startup work, a background job).</summary>
    public const string System = "system";

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken)
    {
        Stamp(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var current = correlation.Current;
        var by = current.Caller?.Name ?? current.Caller?.Id ?? System;
        var via = current.CallerActor;
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedBy = by;
                    entry.Entity.CreatedVia = via;
                    entry.Entity.CreatedUtc = now;
                    entry.Entity.UpdatedBy = by;
                    entry.Entity.UpdatedVia = via;
                    entry.Entity.UpdatedUtc = now;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedBy = by;
                    entry.Entity.UpdatedVia = via;
                    entry.Entity.UpdatedUtc = now;
                    entry.Property(x => x.CreatedBy).IsModified = false;
                    entry.Property(x => x.CreatedVia).IsModified = false;
                    entry.Property(x => x.CreatedUtc).IsModified = false;
                    break;
            }
        }
    }
}
