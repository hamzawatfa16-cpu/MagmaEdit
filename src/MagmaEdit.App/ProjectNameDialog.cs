using Avalonia.Controls;
using Avalonia.Layout;

namespace MagmaEdit.App;

internal sealed class ProjectNameDialog : Window
{
    private readonly TextBox _nameBox;

    private ProjectNameDialog(string initialName)
    {
        Title = "New Project";
        Width = 420;
        Height = 180;
        MinWidth = 360;
        MinHeight = 160;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _nameBox = new TextBox
        {
            Text = initialName,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var createButton = new Button { Content = "Create", IsDefault = true };
        createButton.Click += (_, _) =>
        {
            string name = _nameBox.Text?.Trim() ?? string.Empty;
            if (name.Length == 0)
            {
                _nameBox.Focus();
                return;
            }

            Close(name);
        };

        var cancelButton = new Button { Content = "Cancel", IsCancel = true };
        cancelButton.Click += (_, _) => Close(null);

        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(16),
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = "Project name" },
                _nameBox,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancelButton, createButton }
                }
            }
        };
    }

    public static async Task<string?> ShowAsync(Window owner, string initialName)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ProjectNameDialog dialog = new(initialName);
        return await dialog.ShowDialog<string?>(owner);
    }
}
