using MagmaEdit.AiBridge;

namespace MagmaEdit.Core.Tests;

public sealed class AiBridgeSecurityTests
{
    [Fact]
    public void ValidBearerTokenIsAccepted()
    {
        Assert.True(AiBridgeSecurity.HasValidBearerToken("Bearer secret-token", "secret-token"));
    }

    [Fact]
    public void MissingAuthorizationIsRejected()
    {
        Assert.False(AiBridgeSecurity.HasValidBearerToken(null, "secret-token"));
    }

    [Fact]
    public void WrongSchemeIsRejected()
    {
        Assert.False(AiBridgeSecurity.HasValidBearerToken("Basic secret-token", "secret-token"));
    }

    [Fact]
    public void WrongTokenIsRejected()
    {
        Assert.False(AiBridgeSecurity.HasValidBearerToken("Bearer wrong-token", "secret-token"));
    }

    [Fact]
    public void EmptyExpectedTokenIsRejected()
    {
        Assert.False(AiBridgeSecurity.HasValidBearerToken("Bearer secret-token", string.Empty));
    }

    [Fact]
    public void MutationsRequireServerEnablement()
    {
        Assert.True(AiBridgeSecurity.IsMutationAllowed(false, false));
        Assert.True(AiBridgeSecurity.IsMutationAllowed(false, true));
        Assert.False(AiBridgeSecurity.IsMutationAllowed(true, false));
        Assert.True(AiBridgeSecurity.IsMutationAllowed(true, true));
    }

    [Fact]
    public void BearerPrefixIsCaseSensitive()
    {
        Assert.False(AiBridgeSecurity.HasValidBearerToken("bearer secret-token", "secret-token"));
    }
}
