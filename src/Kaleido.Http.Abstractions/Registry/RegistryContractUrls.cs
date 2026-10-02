namespace Kaleido.Http.Registry;

#pragma warning disable KAL0001 // Pure URL factory — no state, intentional static
internal static class RegistryContractUrls
{
    public static string Registry(string serviceName)
        => string.IsNullOrWhiteSpace(serviceName)
            ? "/registry"
            : $"/{serviceName.Trim().Trim('/')}/registry";
}
