namespace Kaleido.Http;

internal static class KaleidoHttpRoutePrefix
{
    public static string Service(string serviceName, string serviceVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceVersion);
        return $"/{serviceName.Trim().Trim('/')}/{serviceVersion.Trim().Trim('/')}";
    }
}
