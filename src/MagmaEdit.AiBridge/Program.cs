using MagmaEdit.AiBridge;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

AiBridgeOptions options = AiBridgeOptions.FromEnvironment(builder.Environment);
builder.Services.AddSingleton(options);
builder.Services.AddSingleton<OpenAiMcpEditingBridge>();

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
    CancellationToken cancellationToken) =>
{
    if (!AiBridgeSecurity.HasValidBearerToken(
        httpRequest.Headers.Authorization.ToString(),
        bridgeOptions.BridgeBearerToken))
        return Results.Unauthorized();

    if (!AiBridgeSecurity.IsMutationAllowed(request.AllowMutations, bridgeOptions.AllowMutations))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    try
    {
        AiBridgeResult result = await bridge.EditAsync(request, cancellationToken);
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

public static class AiBridgeSecurity
{
    public static bool HasValidBearerToken(string? authorization, string expectedToken)
    {
        const string prefix = "Bearer ";

        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        string suppliedToken = authorization[prefix.Length..];
        if (suppliedToken.Length == 0 || expectedToken.Length == 0 || suppliedToken.Length != expectedToken.Length)
            return false;

        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(suppliedToken),
            System.Text.Encoding.UTF8.GetBytes(expectedToken));
    }

    public static bool IsMutationAllowed(bool requested, bool serverEnabled) => !requested || serverEnabled;
}
