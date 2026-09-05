using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MagmaEdit.Broker;
using MagmaEdit.Integration;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<IMagmaEditPrimaryIdentityValidator, RejectingPrimaryIdentityValidator>();
builder.Services.AddSingleton<IMagmaEditBrokerCredentialStore, InMemoryMagmaEditBrokerCredentialStore>();
builder.Services.AddSingleton<InMemoryMagmaEditReplayProtector>();
builder.Services.AddSingleton<MagmaEditSessionBroker>();

WebApplication app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "MagmaEdit.Broker"
}));

app.MapPost("/v1/broker-credentials/issue", async (
    HttpRequest request,
    IMagmaEditPrimaryIdentityValidator identityValidator,
    IMagmaEditBrokerCredentialStore credentialStore,
    TimeProvider timeProvider,
    CancellationToken cancellationToken) =>
{
    string? userId = await identityValidator.ValidateAsync(
        request.Headers.Authorization.ToString(),
        cancellationToken).ConfigureAwait(false);
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.Unauthorized();
    }

    MagmaEditBrokerCredentialIssue credential = credentialStore.Issue(
        userId,
        timeProvider.GetUtcNow(),
        TimeSpan.FromMinutes(10));
    return Results.Ok(credential);
});

app.MapPost("/v1/broker-credentials/revoke", (
    HttpRequest request,
    IMagmaEditBrokerCredentialStore credentialStore,
    TimeProvider timeProvider) =>
{
    if (!TryGetBearerToken(request, out string accessToken)
        || !credentialStore.TryAuthenticate(accessToken, timeProvider.GetUtcNow(), out string? userId)
        || userId is null)
    {
        return Results.Unauthorized();
    }

    bool revoked = credentialStore.Revoke(accessToken, userId);
    return revoked ? Results.NoContent() : Results.NotFound();
});

app.MapPost("/v1/desktop-sessions/register", (
    HttpRequest request,
    RegistrationEnvelope envelope,
    IMagmaEditBrokerCredentialStore credentialStore,
    InMemoryMagmaEditReplayProtector replayProtector,
    MagmaEditSessionBroker broker,
    TimeProvider timeProvider) =>
    ExecuteAuthenticated(request, envelope.Registration, credentialStore, replayProtector, broker, timeProvider));

app.MapPost("/v1/desktop-sessions/renew", (
    HttpRequest request,
    RenewalEnvelope envelope,
    IMagmaEditBrokerCredentialStore credentialStore,
    InMemoryMagmaEditReplayProtector replayProtector,
    MagmaEditSessionBroker broker,
    TimeProvider timeProvider) =>
{
    if (!TryAuthorizeSessionRequest(request, envelope.UserId, envelope.SessionId, credentialStore, replayProtector, timeProvider, out IResult? failure))
    {
        return failure!;
    }

    try
    {
        bool renewed = broker.TryRenew(
            envelope.UserId,
            envelope.SessionId,
            envelope.LeaseDuration,
            timeProvider.GetUtcNow(),
            out MagmaEditSessionDescriptor? descriptor);
        return renewed && descriptor is not null
            ? Results.Ok(descriptor)
            : Results.NotFound();
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (ArgumentOutOfRangeException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.MapPost("/v1/desktop-sessions/revoke", (
    HttpRequest request,
    RevokeEnvelope envelope,
    IMagmaEditBrokerCredentialStore credentialStore,
    InMemoryMagmaEditReplayProtector replayProtector,
    MagmaEditSessionBroker broker,
    TimeProvider timeProvider) =>
{
    if (!TryAuthorizeSessionRequest(request, envelope.UserId, envelope.SessionId, credentialStore, replayProtector, timeProvider, out IResult? failure))
    {
        return failure!;
    }

    bool removed = broker.Unregister(envelope.UserId, envelope.SessionId);
    return Results.Ok(new UnregisterResponse(removed));
});

app.Run();

static IResult ExecuteAuthenticated(
    HttpRequest request,
    MagmaEditSessionRegistration registration,
    IMagmaEditBrokerCredentialStore credentialStore,
    InMemoryMagmaEditReplayProtector replayProtector,
    MagmaEditSessionBroker broker,
    TimeProvider timeProvider)
{
    if (!TryAuthorizeSessionRequest(request, registration.UserId, registration.SessionId, credentialStore, replayProtector, timeProvider, out IResult? failure))
    {
        return failure!;
    }

    try
    {
        return Results.Ok(broker.Register(registration, timeProvider.GetUtcNow()));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (ArgumentOutOfRangeException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new { error = exception.Message });
    }
}

static bool TryAuthorizeSessionRequest(
    HttpRequest request,
    string userId,
    string sessionId,
    IMagmaEditBrokerCredentialStore credentialStore,
    InMemoryMagmaEditReplayProtector replayProtector,
    TimeProvider timeProvider,
    out IResult? failure)
{
    failure = null;
    if (!TryGetBearerToken(request, out string accessToken)
        || !credentialStore.TryAuthenticate(accessToken, timeProvider.GetUtcNow(), out string? credentialUserId)
        || credentialUserId is null)
    {
        failure = Results.Unauthorized();
        return false;
    }

    if (string.IsNullOrWhiteSpace(userId)
        || !string.Equals(credentialUserId, userId.Trim(), StringComparison.Ordinal))
    {
        failure = Results.StatusCode(StatusCodes.Status403Forbidden);
        return false;
    }

    if (string.IsNullOrWhiteSpace(sessionId))
    {
        failure = Results.BadRequest(new { error = "The session ID is required." });
        return false;
    }

    if (!replayProtector.TryAccept(
        request.Headers["X-MagmaEdit-Request-Id"].ToString(),
        request.Headers["X-MagmaEdit-Timestamp"].ToString(),
        timeProvider.GetUtcNow()))
    {
        failure = Results.StatusCode(StatusCodes.Status409Conflict);
        return false;
    }

    return true;
}

static bool TryGetBearerToken(HttpRequest request, out string accessToken)
{
    accessToken = string.Empty;
    string authorization = request.Headers.Authorization.ToString();
    if (!AuthenticationHeaderValue.TryParse(authorization, out AuthenticationHeaderValue? header)
        || !string.Equals(header.Scheme, "Bearer", StringComparison.Ordinal)
        || string.IsNullOrWhiteSpace(header.Parameter))
    {
        return false;
    }

    accessToken = header.Parameter.Trim();
    return true;
}

public sealed record RegistrationEnvelope(MagmaEditSessionRegistration Registration);
public sealed record RenewalEnvelope(string UserId, string SessionId, TimeSpan LeaseDuration);
public sealed record RevokeEnvelope(string UserId, string SessionId);
public sealed record UnregisterResponse(bool Removed);
