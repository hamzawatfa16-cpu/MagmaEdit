using System.Net.Http.Headers;
using MagmaEdit.Broker;
using MagmaEdit.Integration;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<MagmaEditDesktopRelayHub>();

string? supabaseUrl = Environment.GetEnvironmentVariable("MAGMAEDIT_SUPABASE_URL");
string? publishableKey = Environment.GetEnvironmentVariable("MAGMAEDIT_SUPABASE_PUBLISHABLE_KEY");
if (!string.IsNullOrWhiteSpace(supabaseUrl) && !string.IsNullOrWhiteSpace(publishableKey))
{
    SupabasePrimaryIdentityValidatorOptions options = new(supabaseUrl, publishableKey);
    builder.Services.AddSingleton(options);
    builder.Services.AddHttpClient<SupabasePrimaryIdentityValidator>();
    builder.Services.AddSingleton<IMagmaEditPrimaryIdentityValidator>(serviceProvider =>
        serviceProvider.GetRequiredService<SupabasePrimaryIdentityValidator>());
}
else
{
    builder.Services.AddSingleton<IMagmaEditPrimaryIdentityValidator, RejectingPrimaryIdentityValidator>();
}

string? brokerDatabaseConnection = Environment.GetEnvironmentVariable("MAGMAEDIT_BROKER_DATABASE_CONNECTION");
if (!string.IsNullOrWhiteSpace(brokerDatabaseConnection))
{
    builder.Services.AddSingleton<IMagmaEditBrokerCredentialStore>(_ =>
        new PostgresMagmaEditBrokerCredentialStore(brokerDatabaseConnection));
    builder.Services.AddSingleton<IMagmaEditBrokerReplayProtector>(_ =>
        new PostgresMagmaEditReplayProtector(brokerDatabaseConnection));
    builder.Services.AddSingleton<IMagmaEditSessionStore>(_ =>
        new PostgresMagmaEditSessionStore(brokerDatabaseConnection));
}
else if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IMagmaEditBrokerCredentialStore, InMemoryMagmaEditBrokerCredentialStore>();
    builder.Services.AddSingleton<IMagmaEditBrokerReplayProtector, InMemoryMagmaEditReplayProtector>();
    builder.Services.AddSingleton<IMagmaEditSessionStore, InMemoryMagmaEditSessionStore>();
}
else
{
    throw new InvalidOperationException(
        "MAGMAEDIT_BROKER_DATABASE_CONNECTION is required outside the Development environment.");
}

builder.Services.AddSingleton<MagmaEditSessionBroker>();

WebApplication app = builder.Build();
app.UseWebSockets();

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
    IMagmaEditBrokerReplayProtector replayProtector,
    MagmaEditSessionBroker broker,
    TimeProvider timeProvider) =>
    ExecuteAuthenticated(request, envelope.Registration, credentialStore, replayProtector, broker, timeProvider));

app.MapPost("/v1/desktop-sessions/renew", (
    HttpRequest request,
    RenewalEnvelope envelope,
    IMagmaEditBrokerCredentialStore credentialStore,
    IMagmaEditBrokerReplayProtector replayProtector,
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
    catch (ArgumentOutOfRangeException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.MapPost("/v1/desktop-sessions/revoke", (
    HttpRequest request,
    RevokeEnvelope envelope,
    IMagmaEditBrokerCredentialStore credentialStore,
    IMagmaEditBrokerReplayProtector replayProtector,
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

app.Map("/v1/desktop-sessions/connect", async (
    HttpContext context,
    IMagmaEditBrokerCredentialStore credentialStore,
    IMagmaEditBrokerReplayProtector replayProtector,
    MagmaEditSessionBroker broker,
    MagmaEditDesktopRelayHub relayHub,
    TimeProvider timeProvider,
    CancellationToken cancellationToken) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    string userId = context.Request.Query["userId"].ToString().Trim();
    string sessionId = context.Request.Query["sessionId"].ToString().Trim();
    if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(sessionId))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    if (!TryAuthorizeWebSocketRequest(context.Request, userId, sessionId, credentialStore, replayProtector, broker, timeProvider, out int failureStatus))
    {
        context.Response.StatusCode = failureStatus;
        return;
    }

    using WebSocket socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
    await relayHub.RunDesktopConnectionAsync(userId, sessionId, socket, cancellationToken).ConfigureAwait(false);
});

app.MapPost("/v1/desktop-sessions/relay", async (
    HttpRequest request,
    RelayEnvelope envelope,
    IMagmaEditBrokerCredentialStore credentialStore,
    IMagmaEditBrokerReplayProtector replayProtector,
    MagmaEditSessionBroker broker,
    MagmaEditDesktopRelayHub relayHub,
    TimeProvider timeProvider,
    CancellationToken cancellationToken) =>
{
    if (!TryAuthorizeSessionRequest(request, envelope.UserId, envelope.SessionId, credentialStore, replayProtector, timeProvider, out IResult? failure))
    {
        return failure!;
    }

    if (!broker.TryGet(envelope.UserId, timeProvider.GetUtcNow(), out MagmaEditSessionDescriptor? descriptor)
        || descriptor is null
        || !string.Equals(descriptor.SessionId, envelope.SessionId, StringComparison.Ordinal))
    {
        return Results.NotFound();
    }

    try
    {
        LiveEditorPipeResponse response = await relayHub.RelayAsync(
            envelope.UserId,
            envelope.SessionId,
            envelope.Request with
            {
                UserId = envelope.UserId,
                SessionId = envelope.SessionId
            },
            cancellationToken).ConfigureAwait(false);
        return Results.Ok(response);
    }
    catch (InvalidOperationException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (TimeoutException)
    {
        return Results.StatusCode(StatusCodes.Status504GatewayTimeout);
    }
});

app.Run();

static IResult ExecuteAuthenticated(
    HttpRequest request,
    MagmaEditSessionRegistration registration,
    IMagmaEditBrokerCredentialStore credentialStore,
    IMagmaEditBrokerReplayProtector replayProtector,
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
    catch (ArgumentOutOfRangeException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (ArgumentException exception)
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
    IMagmaEditBrokerReplayProtector replayProtector,
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

static bool TryAuthorizeWebSocketRequest(
    HttpRequest request,
    string userId,
    string sessionId,
    IMagmaEditBrokerCredentialStore credentialStore,
    IMagmaEditBrokerReplayProtector replayProtector,
    MagmaEditSessionBroker broker,
    TimeProvider timeProvider,
    out int failureStatus)
{
    failureStatus = StatusCodes.Status401Unauthorized;
    if (!TryGetBearerToken(request, out string accessToken)
        || !credentialStore.TryAuthenticate(accessToken, timeProvider.GetUtcNow(), out string? credentialUserId)
        || credentialUserId is null)
    {
        return false;
    }

    if (!string.Equals(credentialUserId, userId, StringComparison.Ordinal))
    {
        failureStatus = StatusCodes.Status403Forbidden;
        return false;
    }

    if (!broker.TryGet(userId, timeProvider.GetUtcNow(), out MagmaEditSessionDescriptor? descriptor)
        || descriptor is null
        || !string.Equals(descriptor.SessionId, sessionId, StringComparison.Ordinal))
    {
        failureStatus = StatusCodes.Status404NotFound;
        return false;
    }

    if (!replayProtector.TryAccept(
        request.Headers["X-MagmaEdit-Request-Id"].ToString(),
        request.Headers["X-MagmaEdit-Timestamp"].ToString(),
        timeProvider.GetUtcNow()))
    {
        failureStatus = StatusCodes.Status409Conflict;
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

sealed record RegistrationEnvelope(MagmaEditSessionRegistration Registration);
sealed record RenewalEnvelope(string UserId, string SessionId, TimeSpan LeaseDuration);
sealed record RevokeEnvelope(string UserId, string SessionId);
sealed record UnregisterResponse(bool Removed);
sealed record RelayEnvelope(string UserId, string SessionId, LiveEditorPipeRequest Request);
