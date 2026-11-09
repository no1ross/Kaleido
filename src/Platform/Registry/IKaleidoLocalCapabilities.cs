namespace Kaleido.Platform.Registry;

/// <summary>
/// This host's own capabilities, in the shape the registry publishes them (#553): what it registers with
/// the control registry. Kept by the HTTP transport (<c>AddHttp()</c>), read by the registrar.
/// </summary>
internal interface IKaleidoLocalCapabilities
{
    /// <summary>This host's processors and query sources, no one else's.</summary>
    KaleidoRegistry Capabilities();
}
