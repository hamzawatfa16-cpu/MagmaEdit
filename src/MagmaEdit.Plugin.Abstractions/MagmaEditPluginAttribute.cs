namespace MagmaEdit.Plugin.Abstractions;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MagmaEditPluginAttribute : Attribute
{
    public MagmaEditPluginAttribute(
        string id,
        string name,
        string version,
        string publisher,
        params PluginCapability[] capabilities)
    {
        Id = id;
        Name = name;
        Version = version;
        Publisher = publisher;
        Capabilities = capabilities ?? [];
    }

    public string Id { get; }

    public string Name { get; }

    public string Version { get; }

    public string Publisher { get; }

    public IReadOnlyList<PluginCapability> Capabilities { get; }

    public PluginManifest ToManifest() =>
        new(Id, Name, Version, Publisher, Capabilities.ToArray());
}
