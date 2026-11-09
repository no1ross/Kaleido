using Kaleido.Platform.Registry;

namespace Kaleido.Registry;

internal enum RegistryAnswerKind
{
    Leased,
    Removed,
    Invalid,
    NotAllowed,
    Unknown,
}

// The outcome of a registry write, for the endpoint to answer: a lease, a removal, or a failure with its
// code and message.
internal sealed record RegistryAnswer(RegistryAnswerKind Kind, RegistryLease? Lease = null, string? Code = null, string? Message = null)
{
    public static RegistryAnswer Removed { get; } = new(RegistryAnswerKind.Removed);

    public static RegistryAnswer Leased(RegistryLease lease) => new(RegistryAnswerKind.Leased, lease);

    public static RegistryAnswer Failed(RegistryAnswerKind kind, string code, string message) => new(kind, Code: code, Message: message);
}
