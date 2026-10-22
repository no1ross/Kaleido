namespace Kaleido.Testing;

/// <summary>
/// The one valid/invalid ServiceName table: <c>[a-z0-9][a-z0-9_-]*</c>. Runtime validation
/// (<c>KaleidoServiceOptions.Validate</c>) and the consumer analyzer (KAL2005) are both tested
/// against it, so the build-time and startup rules cannot drift apart. Names stay within the
/// 15-character MCP budget so the length warning (KAL2017) does not mix into these cases.
/// </summary>
internal static class ServiceNameCases
{
    public static TheoryData<string> Valid =>
    [
        "intake",
        "priorauth",
        "intake-events",
        "my_service",
        "my-service_2",
        "v2",
        "2fa",
        "a",
    ];

    public static TheoryData<string> Invalid =>
    [
        "MyService",
        "my service",
        "my\tservice",
        "my\nservice",
        " intake",
        "a/b",
        "a\\b",
        "-intake",
        "_intake",
        "{id}",
        "a?b",
        "a#b",
        "a%20b",
        "a.b",
        "..",
        "a:b",
        "café",
    ];
}
