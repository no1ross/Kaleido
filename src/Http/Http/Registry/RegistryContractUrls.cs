namespace Kaleido.Http.Registry;

internal static class RegistryContractUrls
{
    public static string Registry(string serviceName)
        => string.IsNullOrWhiteSpace(serviceName)
            ? "/registry"
            : $"/{serviceName.Trim().Trim('/')}/registry";
}
