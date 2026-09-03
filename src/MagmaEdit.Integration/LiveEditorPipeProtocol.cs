using System.Text.Json;
using System.Text.Json.Serialization;

namespace MagmaEdit.Integration;

/// <summary>Stable JSON envelope exchanged between the MCP process and the live desktop editor.</summary>
public sealed record LiveEditorPipeRequest(
    string Operation,
    EditorCommandRequest? Command = null,
    string ProtocolVersion = LiveEditorPipeProtocol.Version);

/// <summary>Stable JSON response returned by the live desktop editor.</summary>
public sealed record LiveEditorPipeResponse(
    bool Succeeded,
    string Message,
    EditorCommandResult? CommandResult = null,
    EditorProjectState? State = null,
    string ProtocolVersion = LiveEditorPipeProtocol.Version);

public static class LiveEditorPipeProtocol
{
    public const string Version = "1";
    public const string PipeName = "MagmaEdit.LiveEditor.v1";
    public const string ExecuteOperation = "execute_editor_command";
    public const string GetStateOperation = "get_editor_state";

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}
