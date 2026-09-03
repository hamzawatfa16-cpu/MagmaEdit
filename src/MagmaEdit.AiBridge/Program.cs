using MagmaEdit.AiBridge;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

AiBridgeOptions options = AiBridgeOptions.FromEnvironment(builder.Environment);
builder.Services.AddSingleton(options);
builder.Services.AddSingleton<OpenAiMcpEditingBridge>();
builder.Services.AddSingleton<AiBridgeRateLimiter>();
builder.Services.AddHttpClient<SupabaseUserValidator>(client =>
{
    client.BaseAddress = new Uri(options.SupabaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
});

WebApplication app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "MagmaEdit.AiBridge"
}));

app.MapPost("/v1/edit", async (
    HttpRequest httpRequest,
    AiEditRequest request,
    OpenAiMcpEditingBridge bridge,
    AiBridgeOptions bridgeOptions,
    AiBridgeRateLimiter rateLimiter,
    SupabaseUserValidator userValidator,
    CancellationToken cancellationToken) =>
{
    if (!AiBridgeSecurity.HasValidBearerToken(
        httpRequest.Headers["X-MagmaEdit-Bridge-Token"].ToString(),
        bridgeOptions.BridgeBearerToken))
        return Results.Unauthorized();

    AuthenticatedSupabaseUser? user = await userValidator.ValidateAsync(
        httpRequest.Headers.Authorization.ToString(),
        cancellationToken);
    if (user is null || !AiBridgeSecurity.IsUserAllowed(user.UserId, bridgeOptions.AllowedUserIds))
        return Results.Unauthorized();

    if (!rateLimiter.TryConsume(user.UserId, DateTimeOffset.UtcNow, out TimeSpan retryAfter))
    {
        return Results.Json(
            new
            {
                error = "AI bridge rate limit exceeded.",
                retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            },
            statusCode: StatusCodes.Status429TooManyRequests);
    }

    if (!AiBridgeSecurity.IsMutationAllowed(
        request.AllowMutations,
        bridgeOptions.AllowMutations,
        user.UserId,
        bridgeOptions.AllowedUserIds))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    try
    {
        AiBridgeResult result = await bridge.EditAsync(request, user.UserId, cancellationToken);
        return Results.Ok(result);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.Json(
            new { error = exception.Message },
            statusCode: StatusCodes.Status409Conflict);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
    }
})
.WithName("ExecuteAiEdit");

app.Run();
