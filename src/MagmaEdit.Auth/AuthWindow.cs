using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MagmaEdit.Auth;

public sealed class AuthWindow : Window
{
    private readonly IAuthService _authService;
    private readonly TextBlock _statusText;
    private readonly Button _googleButton;
    private readonly Action _openEditor;
    private bool _completed;

    public AuthWindow(IAuthService authService, Action openEditor)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _openEditor = openEditor ?? throw new ArgumentNullException(nameof(openEditor));

        Title = "Sign in to MagmaEdit";
        Width = 440;
        Height = 360;
        MinWidth = 400;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _statusText = new TextBlock
        {
            Text = "Choose an account to continue.",
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.75
        };

        _googleButton = new Button
        {
            Content = "Continue with Google",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = 46,
            FontSize = 16
        };
        _googleButton.Click += GoogleButton_Click;

        Content = new Border
        {
            Padding = new Thickness(36),
            Child = new StackPanel
            {
                Spacing = 18,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = "MagmaEdit",
                        FontSize = 30,
                        FontWeight = FontWeight.SemiBold,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = "Sign in to use your MagmaEdit account.",
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextAlignment = TextAlignment.Center
                    },
                    _googleButton,
                    _statusText
                }
            }
        };
    }

    private async void GoogleButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_completed)
        {
            return;
        }

        _googleButton.IsEnabled = false;
        _statusText.Text = "Opening Google sign-in...";

        try
        {
            AuthResult result = await _authService.SignInWithGoogleAsync();
            if (!result.Succeeded)
            {
                _statusText.Text = result.Message;
                return;
            }

            _completed = true;
            _statusText.Text = "Signed in. Opening MagmaEdit...";
            _openEditor();
            Close();
        }
        catch (Exception exception)
        {
            _statusText.Text = $"Sign-in failed: {exception.Message}";
        }
        finally
        {
            if (!_completed)
            {
                _googleButton.IsEnabled = true;
            }
        }
    }
}
