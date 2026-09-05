using MagmaEdit.Integration;

namespace MagmaEdit.Broker;

public sealed record RegistrationEnvelope(MagmaEditSessionRegistration Registration);
public sealed record RenewalEnvelope(string UserId, string SessionId, TimeSpan LeaseDuration);
public sealed record RevokeEnvelope(string UserId, string SessionId);
public sealed record UnregisterResponse(bool Removed);
