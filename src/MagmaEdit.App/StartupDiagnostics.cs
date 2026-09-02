using System.Runtime.InteropServices;

namespace MagmaEdit.App;

/// <summary>Records fatal startup failures somewhere the user can inspect after a silent WinExe exit.</summary>
internal static class StartupDiagnostics
{
    private const string ProductName = "MagmaEdit";

    public static string LogPath
    {
        get
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string root = string.IsNullOrWhiteSpace(localAppData)
                ? Path.Combine(Path.GetTempPath(), ProductName)
                : Path.Combine(localAppData, ProductName);
            return Path.Combine(root, "Logs", "startup.log");
        }
    }

    public static void Write(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        string logPath = LogPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(
                logPath,
                $"[{DateTimeOffset.UtcNow:O}] Fatal startup exception{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Diagnostics must never become the cause of another startup failure.
        }

        ShowFatalMessage(logPath, exception);
    }

    public static void WriteComponentFailure(string componentName, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentName);
        ArgumentNullException.ThrowIfNull(exception);

        string logPath = LogPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(
                logPath,
                $"[{DateTimeOffset.UtcNow:O}] Startup component failed: {componentName}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Diagnostics must never become the cause of another startup failure.
        }
    }

    private static void ShowFatalMessage(string logPath, Exception exception)
    {
        try
        {
            _ = MessageBoxW(
                IntPtr.Zero,
                $"MagmaEdit could not start.{Environment.NewLine}{Environment.NewLine}{exception.Message}{Environment.NewLine}{Environment.NewLine}Startup log:{Environment.NewLine}{logPath}",
                ProductName,
                0x00000010 | 0x00010000);
        }
        catch
        {
            // The process is already terminating; do not mask the original startup failure.
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
