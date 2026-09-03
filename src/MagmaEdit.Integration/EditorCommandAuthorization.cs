namespace MagmaEdit.Integration;

/// <summary>Identifies the integration surface requesting an editor command.</summary>
public enum AutomationClientKind
{
    DesktopApp,
    Plugin,
    Mcp
}

/// <summary>Authenticated and capability-scoped identity supplied by a future session layer.</summary>
public sealed record AutomationClientContext(
    string ClientId,
    AutomationClientKind ClientKind,
    IReadOnlySet<EditorCommandCapability> GrantedCapabilities);

/// <summary>Result of checking whether an integration client may execute a command.</summary>
public sealed record EditorCommandAuthorizationResult(
    bool Authorized,
    string Message,
    EditorCommandCapability? RequiredCapability = null)
{
    public static EditorCommandAuthorizationResult Allow() =>
        new(true, string.Empty);

    public static EditorCommandAuthorizationResult Deny(
        string message,
        EditorCommandCapability? requiredCapability = null) =>
        new(false, message, requiredCapability);
}

/// <summary>Applies capability-based authorization before a vendor-neutral command reaches the editor gateway.</summary>
public static class EditorCommandAuthorizer
{
    public static EditorCommandAuthorizationResult Authorize(
        EditorCommandRequest request,
        AutomationClientContext client)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(client);

        if (string.IsNullOrWhiteSpace(client.ClientId))
        {
            return EditorCommandAuthorizationResult.Deny("Automation client identifier is required.");
        }

        ArgumentNullException.ThrowIfNull(client.GrantedCapabilities);

        if (!EditorCommandCatalog.TryGetDefinition(request.Command, out EditorCommandDefinition? definition))
        {
            return EditorCommandAuthorizationResult.Deny("Unsupported editor command.");
        }

        if (client.GrantedCapabilities.Contains(definition.Capability))
        {
            return EditorCommandAuthorizationResult.Allow();
        }

        return EditorCommandAuthorizationResult.Deny(
            $"Client '{client.ClientId}' is not authorized for the '{definition.Capability}' capability.",
            definition.Capability);
    }
}

/// <summary>Routes a command only after the caller passes the capability authorization boundary.</summary>
public sealed class AuthorizedEditorCommandRouter
{
    private readonly EditorCommandRouter _router;

    public AuthorizedEditorCommandRouter(EditorCommandRouter router)
    {
        _router = router ?? throw new ArgumentNullException(nameof(router));
    }

    public EditorCommandResult Execute(
        EditorCommandRequest request,
        AutomationClientContext client)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(client);

        EditorCommandAuthorizationResult authorization = EditorCommandAuthorizer.Authorize(request, client);
        if (!authorization.Authorized)
        {
            return new EditorCommandResult(false, authorization.Message);
        }

        return _router.Execute(request);
    }
}
