namespace Kaleido.Samples.PriorAuth.Compliance;

/// <summary>
/// The sample's own error codes for requests a caller got wrong. They are thrown as
/// <c>KaleidoValidationException</c>, which Kaleido's HTTP layer answers with 400 and this code;
/// any other exception type would surface as a 500 the caller cannot act on. The sample never
/// reuses Kaleido's own codes (<c>qry_*</c>, <c>pro_*</c>): those belong to the framework.
/// </summary>
public static class PriorAuthErrorCodes
{
    /// <summary>A provider search ran for a request whose member (and so its plan) is not captured yet.</summary>
    public const string MemberPlanRequired = "MEMBER_PLAN_REQUIRED";

    /// <summary>A query that needs its parameters (for example the processId it is scoped to) was sent without them.</summary>
    public const string QueryParametersRequired = "QUERY_PARAMETERS_REQUIRED";

    /// <summary>A provider search was sent without the plan whose networks decide network status.</summary>
    public const string PlanIdRequired = "PLAN_ID_REQUIRED";

    /// <summary>
    /// Startup, not a caller error: a host's required connection string is not configured. Thrown as
    /// <c>KaleidoConfigurationException</c>; the sample's own code, so a log reader is not pointed at
    /// one of Kaleido's settings.
    /// </summary>
    public const string MissingConnectionString = "MISSING_CONNECTION_STRING";
}
