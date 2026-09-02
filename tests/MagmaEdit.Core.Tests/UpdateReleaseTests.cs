using MagmaEdit.Core.Updates;

namespace MagmaEdit.Core.Tests;

public sealed class UpdateReleaseTests
{
    [Fact]
    public void ParseAcceptsExpectedStableWindowsInstaller()
    {
        string json = CreateReleaseJson();

        UpdateRelease release = UpdateRelease.Parse(json);

        Assert.Equal(new Version(1, 0, 1), release.Version);
        Assert.Equal("v1.0.1", release.TagName);
        Assert.Equal("MagmaEdit-1.0.1.Setup.exe", release.InstallerName);
        Assert.Equal(12345, release.InstallerSize);
        Assert.Equal(new string('A', 64), release.InstallerSha256);
        Assert.Equal("github-actions[bot]", release.ReleaseAuthor);
        Assert.Equal("github-actions[bot]", release.AssetUploader);
    }

    [Fact]
    public void ParseRejectsPrerelease()
    {
        Assert.Throws<InvalidDataException>(() => UpdateRelease.Parse(CreateReleaseJson(prerelease: true)));
    }

    [Fact]
    public void ParseRejectsMissingInstallerDigest()
    {
        string json = CreateReleaseJson().Replace($"sha256:{new string('A', 64)}", "sha512:abc", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => UpdateRelease.Parse(json));
    }

    [Fact]
    public void ParseRejectsInvalidInstallerName()
    {
        string json = CreateReleaseJson().Replace("MagmaEdit-1.0.1.Setup.exe", "MagmaEdit-1.0.1.malicious.exe", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => UpdateRelease.Parse(json));
    }

    [Fact]
    public void ParseRejectsInvalidDigestLength()
    {
        string json = CreateReleaseJson().Replace(new string('A', 64), "ABC", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => UpdateRelease.Parse(json));
    }

    private static string CreateReleaseJson(bool prerelease = false) => $$"""
        {
          "tag_name": "v1.0.1",
          "draft": false,
          "prerelease": {{prerelease.ToString().ToLowerInvariant()}},
          "author": { "login": "github-actions[bot]" },
          "assets": [
            {
              "name": "MagmaEdit-1.0.1.Setup.exe",
              "browser_download_url": "https://github.com/hamzawatfa16-cpu/MagmaEdit/releases/download/v1.0.1/MagmaEdit-1.0.1.Setup.exe",
              "size": 12345,
              "digest": "sha256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
              "uploader": { "login": "github-actions[bot]" }
            }
          ]
        }
        """;
}
