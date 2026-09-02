using System.Text.Json;
using System.Text.RegularExpressions;

namespace MagmaEdit.Core.Updates;

/// <summary>Validated metadata for one MagmaEdit Windows installer release.</summary>
public sealed record UpdateRelease(
    Version Version,
    string TagName,
    string InstallerName,
    Uri InstallerUri,
    long InstallerSize,
    string InstallerSha256,
    string? ReleaseAuthor,
    string? AssetUploader)
{
    private static readonly Regex Sha256Pattern = new("^[0-9a-f]{64}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static UpdateRelease Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            string tagName = GetRequiredString(root, "tag_name");
            string? author = TryGetNestedLogin(root, "author");
            bool draft = root.TryGetProperty("draft", out JsonElement draftValue) && draftValue.GetBoolean();
            bool prerelease = root.TryGetProperty("prerelease", out JsonElement prereleaseValue) && prereleaseValue.GetBoolean();

            if (draft || prerelease)
                throw new InvalidDataException("The GitHub release is not a stable published release.");

            if (!tagName.StartsWith('v'))
                throw new InvalidDataException("The GitHub release tag is invalid.");

            string versionText = tagName[1..];
            if (!Version.TryParse(versionText, out Version? version) || version is null || version.Revision >= 0)
            {
                throw new InvalidDataException("The GitHub release does not use a supported semantic version.");
            }

            if (root.TryGetProperty("assets", out JsonElement assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement asset in assets.EnumerateArray())
                {
                    string? name = TryGetString(asset, "name");
                    if (!string.Equals(name, $"MagmaEdit-{version}.Setup.exe", StringComparison.Ordinal))
                        continue;

                    string browserDownloadUrl = GetRequiredString(asset, "browser_download_url");
                    if (!Uri.TryCreate(browserDownloadUrl, UriKind.Absolute, out Uri? installerUri))
                        throw new InvalidDataException("The GitHub release installer URL is invalid.");

                    long size = GetRequiredInt64(asset, "size");
                    string digest = GetRequiredString(asset, "digest");
                    if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The GitHub release installer is missing a SHA-256 digest.");

                    string sha256 = digest[7..];
                    if (!Sha256Pattern.IsMatch(sha256))
                        throw new InvalidDataException("The GitHub release installer SHA-256 digest is invalid.");

                    string? uploader = TryGetNestedLogin(asset, "uploader");
                    return new UpdateRelease(version, tagName, name, installerUri, size, sha256.ToUpperInvariant(), author, uploader);
                }
            }
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The GitHub release response was not valid JSON.", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException("The GitHub release response had an invalid shape.", exception);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The GitHub release response contained an invalid value.", exception);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException("The GitHub release response contained an out-of-range value.", exception);
        }

        throw new InvalidDataException("The GitHub release does not contain the expected MagmaEdit Windows installer.");
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        string? value = TryGetString(element, propertyName);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"The GitHub release is missing '{propertyName}'.")
            : value;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static long GetRequiredInt64(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.Number)
            throw new InvalidDataException($"The GitHub release is missing '{propertyName}'.");

        long result = value.GetInt64();
        if (result <= 0)
            throw new InvalidDataException($"The GitHub release '{propertyName}' must be positive.");
        return result;
    }

    private static string? TryGetNestedLogin(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.Object)
            return null;

        return TryGetString(value, "login");
    }
}
