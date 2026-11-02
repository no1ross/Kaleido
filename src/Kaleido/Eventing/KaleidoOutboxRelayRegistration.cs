namespace Kaleido.Eventing;

// The relay AddOutboxRelay registered in this host, with its publisher: a host runs one (#552).
[ExcludeFromCodeCoverage]
internal sealed record KaleidoOutboxRelayRegistration(Type Publisher);
