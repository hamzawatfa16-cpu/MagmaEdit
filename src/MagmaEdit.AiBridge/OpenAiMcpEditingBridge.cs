#pragma warning disable OPENAI001

using OpenAI.Responses;

namespace MagmaEdit.AiBridge;

public sealed partial class OpenAiMcpEditingBridge
{
    private const string ReadOnlyTool = "magmaedit.get_editor_state";
    private const string EditingTool = "magmaedit.execute_editor_command";

    private readonly AiBridgeOptions _options;
    private readonly ILogger<OpenAiMcpEditingBridge> _logger;
    private readonly ResponsesClient _client;

    public OpenAiMcpEditingBridge(
        AiBridgeOptions options,
        ILogger<OpenAiMcpEditingBridge> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _client = new ResponsesClient(_options.OpenAiApiKey);
    }

    public async Task<AiBridgeResult> EditAsync(
        AiEditRequest request,
        string userId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Prompt);

        if (request.Prompt.Length > 12000)
        {
            throw new ArgumentException(
                "Prompt is too large. Maximum length is 12000 characters.",
                nameof(request));
        }

        if (request.AllowMutations && !_options.AllowMutations)
        {
            throw new InvalidOperationException(
                "Mutating AI commands are disabled by MAGMAEDIT_AI_BRIDGE_ALLOW_MUTATIONS.");
        }

        CreateResponseOptions options = new()
        {
            Model = _options.OpenAiModel,
            PreviousResponseId = request.PreviousResponseId
        };

        McpToolFilter allowedTools = new();
        allowedTools.ToolNames.Add(ReadOnlyTool);
        if (request.AllowMutations)
            allowedTools.ToolNames.Add(EditingTool);

        options.Tools.Add(ResponseTool.CreateMcpTool(
            serverLabel: "magmaedit",
            serverUri: new Uri(_options.RemoteMcpUrl, UriKind.Absolute),
            allowedTools: allowedTools,
            authorizationToken: _options.RemoteMcpBearerToken,
            toolCallApprovalPolicy: request.AllowMutations
                ? GlobalMcpToolCallApprovalPolicy.NeverRequireApproval
                : GlobalMcpToolCallApprovalPolicy.AlwaysRequireApproval));

        options.InputItems.Add(ResponseItem.CreateUserMessageItem(BuildPrompt(request)));

        LogStarting(userId, request.AllowMutations, _options.OpenAiModel);

        ResponseResult response = await _client.CreateResponseAsync(options, cancellationToken);

        string output = response.GetOutputText();
        LogCompleted(userId, response.Id, output.Length);

        return new AiBridgeResult(
            response.Id,
            output,
            request.AllowMutations,
            response.OutputItems.Count);
    }

    public Task<AiBridgeResult> EditAsync(
        AiEditRequest request,
        CancellationToken cancellationToken)
    {
        return EditAsync(request, "legacy", cancellationToken);
    }

    private static string BuildPrompt(AiEditRequest request)
    {
        const string policy = "You are MagmaEdit's editing assistant. Use only the provided MagmaEdit MCP tools for editor state or editor mutations. Do not invent project state. Before making a mutation, reason from the current editor state. Keep edits inside the user's requested scope. After completing requested edits, briefly report what changed.";

        return request.AllowMutations
            ? $"{policy}\n\nThe user has explicitly enabled editing actions for this request.\n\nUser request:\n{request.Prompt}"
            : $"{policy}\n\nThis request is read-only. Do not perform mutations. You may inspect editor state and explain what would need to change.\n\nUser request:\n{request.Prompt}";
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Starting AI edit request. UserId={UserId}, MutationsEnabled={MutationsEnabled}, Model={Model}")]
    private partial void LogStarting(string userId, bool mutationsEnabled, string model);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "AI edit request completed. UserId={UserId}, ResponseId={ResponseId}, OutputLength={OutputLength}")]
    private partial void LogCompleted(string userId, string responseId, int outputLength);
}

public sealed record AiEditRequest(
    string Prompt,
    string? PreviousResponseId = null,
    bool AllowMutations = false);

public sealed record AiBridgeResult(
    string ResponseId,
    string OutputText,
    bool MutationsEnabled,
    int OutputItemCount);
