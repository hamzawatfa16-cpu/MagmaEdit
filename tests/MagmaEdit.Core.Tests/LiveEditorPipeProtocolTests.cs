using System.Text.Json;
using MagmaEdit.Integration;

namespace MagmaEdit.Core.Tests;

public sealed class LiveEditorPipeProtocolTests
{
    [Fact]
    public void UserIdRoundTripsThroughProtocolJson()
    {
        var request = new LiveEditorPipeRequest(
            LiveEditorPipeProtocol.GetStateOperation,
            UserId: "user-123");

        string json = JsonSerializer.Serialize(request, LiveEditorPipeProtocol.JsonOptions);
        LiveEditorPipeRequest? restored = JsonSerializer.Deserialize<LiveEditorPipeRequest>(
            json,
            LiveEditorPipeProtocol.JsonOptions);

        Assert.NotNull(restored);
        Assert.Equal("user-123", restored.UserId);
        Assert.Equal(LiveEditorPipeProtocol.GetStateOperation, restored.Operation);
        Assert.Equal(LiveEditorPipeProtocol.Version, restored.ProtocolVersion);
    }

    [Fact]
    public void MissingUserIdRemainsCompatibleWithProtocolDefaults()
    {
        var request = new LiveEditorPipeRequest(LiveEditorPipeProtocol.GetStateOperation);

        string json = JsonSerializer.Serialize(request, LiveEditorPipeProtocol.JsonOptions);
        LiveEditorPipeRequest? restored = JsonSerializer.Deserialize<LiveEditorPipeRequest>(
            json,
            LiveEditorPipeProtocol.JsonOptions);

        Assert.NotNull(restored);
        Assert.Null(restored.UserId);
    }
}
