namespace Kaleido.Http.Registry;

/// <summary>
/// Stable codes of the control registry's endpoints (#553), answered in a <c>KaleidoErrorResponse</c>.
/// </summary>
public static class RegistryErrorCodes
{
    /// <summary>
    /// 403: the caller may not register, renew or remove an instance of that service. The registry's
    /// <c>Kaleido:Registry:Registrants</c> maps each service to the client ids allowed to register it.
    /// </summary>
    public const string RegistrantNotAllowed = "registry_registrant_not_allowed";

    /// <summary>
    /// 403: the caller may not read the control registry. Only other Kaleido hosts read it - those
    /// satisfying <c>Kaleido:Registry:ReaderPolicy</c>; users read a router's or orchestrator's own registry.
    /// </summary>
    public const string ReaderNotAllowed = "registry_reader_not_allowed";

    /// <summary>
    /// 404: the registry does not know the instance (it restarted, failed over or removed it). The
    /// instance registers again.
    /// </summary>
    public const string InstanceUnknown = "registry_instance_unknown";

    /// <summary>
    /// 400: the registration is not one the registry can keep: no service version, an invalid service name
    /// or version, an empty instance id, or a capability of another service.
    /// </summary>
    public const string InvalidRegistration = "registry_invalid_registration";
}
