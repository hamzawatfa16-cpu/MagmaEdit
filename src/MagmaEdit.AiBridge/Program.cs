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
