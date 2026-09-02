using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using MagmaEdit.Core.Updates;

namespace MagmaEdit.App;

/// <summary>Checks and installs stable MagmaEdit releases from the project's GitHub repository.</summary>
internal sealed class UpdateService
{
    private const string Owner = "hamzawatfa16-cpu";
    private const string Repository = "MagmaEdit";
    private const string RequiredReleaseAuthor = "github-actions[bot]";
    private const long MaximumInstallerBytes = 250L * 1024 * 1024;
    private const int MaximumDownloadRedirects = 3;

    private static readonly Uri LatestReleaseUri = new($"https://api.github.com/repos/{Owner}/{Repository}/releases/latest");
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public Version CurrentVersion { get; }

    public UpdateService()
    {
        CurrentVersion = typeof(UpdateService).Assembly.GetName().Version is { } version
            ? new Version(version.Major, version.Minor, Math.Max(0, version.Build))
            : new Version(1, 0, 0);
    }

    public async Task<UpdateRelease?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await HttpClient.GetAsync(LatestReleaseUri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        UpdateRelease release = UpdateRelease.Parse(json);
        ValidateReleaseTrust(release);
        return release.Version > CurrentVersion ? release : null;
    }

    public async Task InstallAsync(UpdateRelease release, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        ValidateReleaseTrust(release);

        if (release.Version <= CurrentVersion)
            throw new InvalidOperationException("The requested update is not newer than the installed version.");
        if (release.InstallerSize <= 0 || release.InstallerSize > MaximumInstallerBytes)
            throw new InvalidDataException("The update installer size is outside the supported safety limit.");

        string updateDirectory = Path.Combine(Path.GetTempPath(), "MagmaEdit", "Updates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(updateDirectory);
        string installerPath = Path.Combine(updateDirectory, release.InstallerName);

        try
        {
            using HttpResponseMessage response = await GetInstallerResponseAsync(release.InstallerUri, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength is long declaredSize && declaredSize != release.InstallerSize)
                throw new InvalidDataException("The downloaded update size does not match the GitHub release metadata.");

            await DownloadAndVerifyAsync(response, installerPath, release, cancellationToken).ConfigureAwait(false);

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true,
                WorkingDirectory = updateDirectory,
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS"
            };

            if (System.Diagnostics.Process.Start(startInfo) is null)
                throw new InvalidOperationException("Windows could not start the MagmaEdit installer.");
        }
        catch
        {
            TryDeleteDirectory(updateDirectory);
            throw;
        }
    }

    private static async Task<HttpResponseMessage> GetInstallerResponseAsync(Uri installerUri, CancellationToken cancellationToken)
    {
        Uri currentUri = installerUri;

        for (int redirectCount = 0; ; redirectCount++)
        {
            ValidateDownloadUri(currentUri, installerUri);

            using HttpRequestMessage request = new(HttpMethod.Get, currentUri);
            HttpResponseMessage response = await HttpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (!IsRedirect(response.StatusCode))
                return response;

            if (redirectCount >= MaximumDownloadRedirects)
            {
                response.Dispose();
                throw new InvalidDataException("The update download followed too many redirects.");
            }

            Uri? nextUri = response.Headers.Location;
            response.Dispose();
            if (nextUri is null)
                throw new InvalidDataException("The update download returned a redirect without a destination.");

            currentUri = new Uri(currentUri, nextUri);
        }
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.MovedPermanently or
            HttpStatusCode.Found or
            HttpStatusCode.TemporaryRedirect or
            HttpStatusCode.PermanentRedirect;

    private static async Task DownloadAndVerifyAsync(
        HttpResponseMessage response,
        string destination,
        UpdateRelease release,
        CancellationToken cancellationToken)
    {
        await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using FileStream destinationStream = new(
            destination,
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.SequentialScan,
                BufferSize = 64 * 1024
            });

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[64 * 1024];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total = checked(total + read);
            if (total > release.InstallerSize || total > MaximumInstallerBytes)
                throw new InvalidDataException("The downloaded update exceeds the release size limit.");

            await destinationStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            hash.AppendData(buffer, 0, read);
        }

        await destinationStream.FlushAsync(cancellationToken).ConfigureAwait(false);

        if (total != release.InstallerSize)
            throw new InvalidDataException("The downloaded update is incomplete.");

        string actualHash = Convert.ToHexString(hash.GetHashAndReset());
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(actualHash),
                Encoding.ASCII.GetBytes(release.InstallerSha256)))
        {
            throw new InvalidDataException("The downloaded update failed SHA-256 verification.");
        }

        await using FileStream verifyStream = new(destination, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] magic = new byte[2];
        int magicBytes = await verifyStream.ReadAsync(magic, cancellationToken).ConfigureAwait(false);
        if (magicBytes != 2 || magic[0] != (byte)'M' || magic[1] != (byte)'Z')
            throw new InvalidDataException("The downloaded update is not a valid Windows executable.");
    }

    private static void ValidateReleaseTrust(UpdateRelease release)
    {
        if (!string.Equals(release.ReleaseAuthor, RequiredReleaseAuthor, StringComparison.Ordinal) ||
            !string.Equals(release.AssetUploader, RequiredReleaseAuthor, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The MagmaEdit update release provenance could not be trusted.");
        }

        ValidateDownloadUri(release.InstallerUri, release.InstallerUri);

        string expectedPath = $"/{Owner}/{Repository}/releases/download/{release.TagName}/{release.InstallerName}";
        if (!string.Equals(release.InstallerUri.AbsolutePath, expectedPath, StringComparison.Ordinal))
            throw new InvalidDataException("The MagmaEdit update installer URL does not match its GitHub release.");
    }

    private static void ValidateDownloadUri(Uri uri, Uri originalInstallerUri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps || !IsAllowedDownloadHost(uri.Host))
            throw new InvalidDataException("The MagmaEdit update installer URL is not an allowed HTTPS GitHub download.");

        if (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
            !uri.Host.Equals(originalInstallerUri.Host, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The MagmaEdit update redirect returned to an unexpected GitHub host.");
    }

    private static bool IsAllowedDownloadHost(string host) =>
        host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MagmaEdit", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2026-03-10");
        return client;
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
