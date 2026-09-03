using System.Security.Cryptography;
using System.Text;
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
    if (!HasValidBearerToken(httpRequest, bridgeOptions.BridgeBearerToken))
        return Results.Unauthorized();

    if (request.AllowMutations && !bridgeOptions.AllowMutations)
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

static bool HasValidBearerToken(HttpRequest request, string expectedToken)
{
    const string prefix = "Bearer ";

    if (!request.Headers.TryGetValue("Authorization", out var authorizationValues))
        return false;

    string authorization = authorizationValues.ToString();
    if (!authorization.StartsWith(prefix, StringComparison.Ordinal))
        return false;

    string suppliedToken = authorization[prefix.Length..];
    if (suppliedToken.Length == 0 || expectedToken.Length == 0)
        return false;

    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(suppliedToken),
        Encoding.UTF8.GetBytes(expectedToken));
}
