using System.Reflection;

namespace MagmaEdit.App;

/// <summary>Refreshes the existing code-built editor UI after a live automation mutation.</summary>
internal static class LiveEditorPipeUiRefresh
{
    private static readonly string[] RefreshMethods =
    [
        "LoadMediaItems",
        "RefreshTimeline",
        "UpdateHistoryButtons",
        "UpdateClipActionButtons"
    ];

    public static void Refresh(MainWindow window, string message)
    {
        ArgumentNullException.ThrowIfNull(window);

        foreach (string methodName in RefreshMethods)
        {
            MethodInfo method = typeof(MainWindow).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(typeof(MainWindow).FullName, methodName);
            method.Invoke(window, null);
        }

        window.SetStatusForGallery(message);
        window.InvalidateVisual();
    }
}
