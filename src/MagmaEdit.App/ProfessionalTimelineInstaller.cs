using Avalonia.Controls;
using Avalonia.VisualTree;

namespace MagmaEdit.App;

/// <summary>Replaces the legacy timeline list with the professional timeline surface.</summary>
internal static class ProfessionalTimelineInstaller
{
    public static void Attach(MainWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        Border? timelineBorder = window
            .GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(text => string.Equals(text.Text, "Timeline", StringComparison.Ordinal))
            .Select(text => FindParentBorder(text))
            .FirstOrDefault(border => border is not null);

        if (timelineBorder is null)
        {
            throw new InvalidOperationException("Could not locate the timeline host in the editor window.");
        }

        timelineBorder.Child = new ProfessionalTimelineView(
            window.GetProjectForExport,
            window.SetStatusForGallery);
    }

    private static Border? FindParentBorder(Visual visual)
    {
        Visual? current = visual;
        while (current is not null)
        {
            if (current is Border border)
            {
                return border;
            }

            current = current.GetVisualParent();
        }

        return null;
    }
}
