using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Testing.Registry;

public sealed class RegistryFakeClock(FakeTimeProvider time) : ISystemClock
{
    public DateTimeOffset UtcNow => time.GetUtcNow();
}
