namespace Kaleido.Samples.PriorAuth.Audit;

/// <summary>
/// An entity whose rows record who created and last changed them, through which application, and when.
/// <see cref="AuditSaveChangesInterceptor"/> stamps the fields on every save; handlers never set them.
/// </summary>
/// <remarks>
/// "Who" is the caller Kaleido read from the request's token: the user on their own call and on an
/// on-behalf-of hop, <c>svc-{service}</c> on a service's own call, <c>system</c> when there is no caller.
/// "Via" is the application that made this hop (the token's actor claim, Kaleido's <c>CallerActor</c>):
/// for example <c>intake</c> when Intake hands a user's request to Radiology; empty on a direct user call.
/// </remarks>
public interface IAuditable
{
    /// <summary>The caller that created the row.</summary>
    string CreatedBy { get; set; }

    /// <summary>The application that made the call that created the row, if any.</summary>
    string? CreatedVia { get; set; }

    /// <summary>When the row was created, in UTC.</summary>
    DateTimeOffset CreatedUtc { get; set; }

    /// <summary>The caller that last changed the row (the creator until it changes).</summary>
    string UpdatedBy { get; set; }

    /// <summary>The application that made the call that last changed the row, if any.</summary>
    string? UpdatedVia { get; set; }

    /// <summary>When the row last changed, in UTC (its creation time until it changes).</summary>
    DateTimeOffset UpdatedUtc { get; set; }
}
