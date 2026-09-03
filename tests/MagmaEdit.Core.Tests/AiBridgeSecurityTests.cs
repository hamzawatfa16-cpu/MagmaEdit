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
    public void UserAllowlistDefaultsToOpenWhenEmpty()
    {
        HashSet<string> allowedUserIds = new(StringComparer.Ordinal);

        Assert.True(AiBridgeSecurity.IsUserAllowed("user-a", allowedUserIds));
    }

    [Fact]
    public void UserAllowlistRejectsUnknownUser()
    {
        HashSet<string> allowedUserIds = ["user-a"];

        Assert.False(AiBridgeSecurity.IsUserAllowed("user-b", allowedUserIds));
        Assert.True(AiBridgeSecurity.IsUserAllowed("user-a", allowedUserIds));
    }

    [Fact]
    public void MutationsRequireSingleAuthorizedUser()
    {
        HashSet<string> allowedUserIds = ["user-a"];

        Assert.True(AiBridgeSecurity.IsMutationAllowed(true, true, "user-a", allowedUserIds));
        Assert.False(AiBridgeSecurity.IsMutationAllowed(true, true, "user-b", allowedUserIds));
    }

    [Fact]
    public void MutationsRemainDisabledForMultipleAuthorizedUsers()
    {
        HashSet<string> allowedUserIds = ["user-a", "user-b"];

        Assert.False(AiBridgeSecurity.IsMutationAllowed(true, true, "user-a", allowedUserIds));
    }

    [Fact]
    public void BearerPrefixIsCaseSensitive()
    {
        Assert.False(AiBridgeSecurity.HasValidBearerToken("bearer secret-token", "secret-token"));
    }
}
