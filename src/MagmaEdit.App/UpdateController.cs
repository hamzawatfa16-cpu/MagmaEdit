using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MagmaEdit.App;

/// <summary>Adds a one-click update action to the application header.</summary>
internal sealed class UpdateController
{
    private readonly Button _button;
    private readonly UpdateService _service;
    private bool _busy;

    private UpdateController(Button button)
    {
        _button = button;
        _service = new UpdateService();
        _button.Click += UpdateButton_Click;
    }

    public static UpdateController Attach(MainWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (window.Content is not Grid root || root.Children.Count == 0 || root.Children[0] is not StackPanel header)
            throw new InvalidOperationException("MagmaEdit's main layout does not expose its action header.");

        Button button = new()
        {
            Content = "Update"
        };
        header.Children.Add(button);
        return new UpdateController(button);
    }

    private async void UpdateButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_busy)
            return;

        _busy = true;
        _button.IsEnabled = false;
        _button.Content = "Checking…";

        try
        {
            MagmaEdit.Core.Updates.UpdateRelease? update = await _service.CheckForUpdateAsync().ConfigureAwait(true);
            if (update is null)
            {
                _button.Content = "Up to date ✓";
                await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(true);
                return;
            }

            _button.Content = $"Updating to {update.Version}…";
            await _service.InstallAsync(update).ConfigureAwait(true);
            _button.Content = "Restarting…";

            await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(true);
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }
        catch (HttpRequestException)
        {
            _button.Content = "Update unavailable";
            await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(true);
        }
        catch (TaskCanceledException)
        {
            _button.Content = "Update timed out";
            await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(true);
        }
        catch (InvalidDataException)
        {
            _button.Content = "Update rejected";
            await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(true);
        }
        catch (InvalidOperationException)
        {
            _button.Content = "Update failed";
            await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(true);
        }
        catch (IOException)
        {
            _button.Content = "Update failed";
            await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(true);
        }
        finally
        {
            _busy = false;
            _button.IsEnabled = true;
            _button.Content = "Update";
        }
    }
}
