using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Samples.PriorAuth.Audit;

/// <summary>
/// Audit fields on the services' own databases (Intake, Radiology, History): who created and last
/// changed each row, through which application, and when (<see cref="IAuditable"/>).
/// </summary>
public static class PriorAuthAuditing
{
    /// <summary>
    /// Registers <see cref="AuditSaveChangesInterceptor"/> per request scope; add it to the context with
    /// <see cref="UseAuditing"/>.
    /// </summary>
    public static IServiceCollection AddPriorAuthAuditing(this IServiceCollection services) =>
        services.AddScoped<AuditSaveChangesInterceptor>();

    /// <summary>Adds the request's <see cref="AuditSaveChangesInterceptor"/> to a context's options.</summary>
    public static DbContextOptionsBuilder UseAuditing(this DbContextOptionsBuilder options, IServiceProvider services) =>
        options.AddInterceptors(services.GetRequiredService<AuditSaveChangesInterceptor>());

    /// <summary>
    /// Maps the audit columns of every <see cref="IAuditable"/> entity in the model: names limited to
    /// 200 characters, times stored as ticks (SQLite cannot order a <see cref="DateTimeOffset"/>).
    /// </summary>
    public static ModelBuilder ApplyAuditColumns(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(x => typeof(IAuditable).IsAssignableFrom(x.ClrType)))
        {
            var entity = modelBuilder.Entity(entityType.ClrType);
            entity.Property<string>(nameof(IAuditable.CreatedBy)).HasMaxLength(200).IsRequired();
            entity.Property<string>(nameof(IAuditable.CreatedVia)).HasMaxLength(200);
            entity.Property<DateTimeOffset>(nameof(IAuditable.CreatedUtc)).HasConversion<long>();
            entity.Property<string>(nameof(IAuditable.UpdatedBy)).HasMaxLength(200).IsRequired();
            entity.Property<string>(nameof(IAuditable.UpdatedVia)).HasMaxLength(200);
            entity.Property<DateTimeOffset>(nameof(IAuditable.UpdatedUtc)).HasConversion<long>();
        }

        return modelBuilder;
    }
}
