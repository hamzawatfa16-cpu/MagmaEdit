using System.Text.Json;
using MagmaEdit.Integration;

namespace MagmaEdit.Core.Tests;

public sealed class LiveEditorPipeProtocolTests
{
    [Fact]
    public void UserAndSessionIdsRoundTripThroughProtocolJson()
    {
        var request = new LiveEditorPipeRequest(
            LiveEditorPipeProtocol.GetStateOperation,
            UserId: "user-123",
            SessionId: "session-456");

        string json = JsonSerializer.Serialize(request, LiveEditorPipeProtocol.JsonOptions);
        LiveEditorPipeRequest? restored = JsonSerializer.Deserialize<LiveEditorPipeRequest>(
            json,
            LiveEditorPipeProtocol.JsonOptions);

        Assert.NotNull(restored);
        Assert.Equal("user-123", restored.UserId);
        Assert.Equal("session-456", restored.SessionId);
        Assert.Equal(LiveEditorPipeProtocol.GetStateOperation, restored.Operation);
        Assert.Equal(LiveEditorPipeProtocol.Version, restored.ProtocolVersion);
    }

    [Fact]
    public void MissingIdentityRemainsCompatibleWithProtocolDefaults()
    {
        var request = new LiveEditorPipeRequest(LiveEditorPipeProtocol.GetStateOperation);

        string json = JsonSerializer.Serialize(request, LiveEditorPipeProtocol.JsonOptions);
        LiveEditorPipeRequest? restored = JsonSerializer.Deserialize<LiveEditorPipeRequest>(
            json,
            LiveEditorPipeProtocol.JsonOptions);

        Assert.NotNull(restored);
        Assert.Null(restored.UserId);
        Assert.Null(restored.SessionId);
    }
}
