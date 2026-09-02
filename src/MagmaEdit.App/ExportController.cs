using System.Globalization;
using Avalonia.Controls;
using MagmaEdit.Core.Export;
using MagmaEdit.Core.Projects;
using MagmaEdit.Core.Workspace;

namespace MagmaEdit.App;

/// <summary>Adds a real one-click export action to the existing window without duplicating project state.</summary>
internal sealed class ExportController
{
    private readonly WorkspaceLayout _workspace = WorkspaceLayout.ForCurrentUser();
    private readonly Button _button;
    private bool _exporting;

    private ExportController(Button button)
    {
        _button = button;
        _button.Click += ExportButton_Click;
    }

    public static ExportController Attach(MainWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (window.Content is not Grid root || root.Children.Count == 0 || root.Children[0] is not StackPanel header)
            throw new InvalidOperationException("MagmaEdit's main layout does not expose its action header.");

        Button button = new()
        {
            Content = "Export MP4"
        };
        header.Children.Add(button);
        return new ExportController(button);
    }

    private async void ExportButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_exporting)
            return;

        _exporting = true;
        _button.IsEnabled = false;
        _button.Content = "Exporting…";

        try
        {
            new WorkspaceManager(_workspace).EnsureCreated();
            ProjectStore store = new(_workspace);
            string projectPath = store.GetDefaultPath("Untitled Project");
            ProjectDocument project = ProjectStore.Load(projectPath);

            string fileName = BuildExportFileName(project.Name);
            string outputPath = Path.Combine(_workspace.Exports, fileName);
            var exporter = new VideoExportService();
            await exporter.ExportAsync(project, outputPath).ConfigureAwait(true);

            _button.Content = "Exported ✓";
            await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(true);
        }
        catch (Exception exception) when (
            exception is InvalidDataException or
            InvalidOperationException or
            NotSupportedException or
            FileNotFoundException or
            IOException or
            UnauthorizedAccessException)
        {
            _button.Content = "Export Failed";
            await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(true);
        }
        finally
        {
            _exporting = false;
            _button.IsEnabled = true;
            _button.Content = "Export MP4";
        }
    }

    private static string BuildExportFileName(string projectName)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        HashSet<char> invalidSet = invalid.ToHashSet();
        string safeName = new(projectName.Trim().Select(character => invalidSet.Contains(character) ? '_' : character).ToArray());
        safeName = safeName.TrimEnd('.', ' ');
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = "Untitled Project";

        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return $"{safeName} - {timestamp}.mp4";
    }
}
