namespace Kaleido.Http.Client;

// How long a host waits before trying the control registry again when it has nothing from the registry to
// follow yet, or a call failed (#553): 1 second, doubling, at most 30 seconds.
internal static class RegistryRetry
{
    internal static readonly TimeSpan First = TimeSpan.FromSeconds(1);
    internal static readonly TimeSpan Longest = TimeSpan.FromSeconds(30);

    internal static TimeSpan After(int attempt) =>
        TimeSpan.FromTicks(Math.Min(Longest.Ticks, First.Ticks << Math.Clamp(attempt, 0, 10)));
}
