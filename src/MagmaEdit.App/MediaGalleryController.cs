using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MagmaEdit.Core.Media;
using MagmaEdit.Core.Projects;

namespace MagmaEdit.App;

/// <summary>Owns the video-library gallery controls, ordering, search, filtering, thumbnails, and publish state UI.</summary>
public sealed class MediaGalleryController : IDisposable
{
    private readonly Window _window;
    private readonly WrapPanel _host;
    private readonly Func<IReadOnlyList<MediaAsset>> _getAssets;
    private readonly Action<MediaAsset> _selectMedia;
    private readonly Action _saveProject;
    private readonly Action<MediaAsset> _removeMedia;
    private readonly Action<string> _setStatus;
    private readonly TextBox _searchBox;
    private readonly ComboBox _sortBox;
    private readonly ComboBox _statusFilterBox;
    private readonly Dictionary<string, Bitmap> _thumbnailCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Bitmap _placeholderBitmap;
    private bool _refreshing;
    private bool _disposed;

    private MediaGalleryController(
        Window window,
        WrapPanel host,
        Func<IReadOnlyList<MediaAsset>> getAssets,
        Action<MediaAsset> selectMedia,
        Action saveProject,
        Action<MediaAsset> removeMedia,
        Action<string> setStatus)
    {
        _window = window;
        _host = host;
        _getAssets = getAssets;
        _selectMedia = selectMedia;
        _saveProject = saveProject;
        _removeMedia = removeMedia;
        _setStatus = setStatus;

        _searchBox = new TextBox
        {
            PlaceholderText = "Search videos by name…",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 110
        };
        _searchBox.TextChanged += SearchBox_TextChanged;

        _sortBox = new ComboBox
        {
            SelectedIndex = 0,
            ItemsSource = new[] { "Newest", "Oldest" },
            MinWidth = 110
        };
        _sortBox.SelectionChanged += FilterChanged;

        _statusFilterBox = new ComboBox
        {
            SelectedIndex = 0,
            ItemsSource = new[] { "All", "Published", "Not Published" },
            MinWidth = 130
        };
        _statusFilterBox.SelectionChanged += FilterChanged;

        _placeholderBitmap = CreatePlaceholderBitmap();
        _window.Closed += Window_Closed;
        Refresh();
    }

    public static MediaGalleryController Attach(
        Window window,
        Func<IReadOnlyList<MediaAsset>> getAssets,
        Action<MediaAsset> selectMedia,
        Action saveProject,
        Action<MediaAsset> removeMedia,
        Action<string> setStatus)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(getAssets);
        ArgumentNullException.ThrowIfNull(selectMedia);
        ArgumentNullException.ThrowIfNull(saveProject);
        ArgumentNullException.ThrowIfNull(removeMedia);
        ArgumentNullException.ThrowIfNull(setStatus);
        return CreateForWindow(window, getAssets, selectMedia, saveProject, removeMedia, setStatus);
    }

    public static MediaGalleryController Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (window.GetType() != typeof(MainWindow))
        {
            throw new InvalidOperationException("The automatic gallery attachment requires MagmaEdit.MainWindow.");
        }

        Type type = typeof(MainWindow);
        FieldInfo projectField = type.GetField("_project", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("MainWindow project field was not found.");
        FieldInfo statusField = type.GetField("_statusText", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("MainWindow status field was not found.");
        MethodInfo selectMethod = type.GetMethod("SelectMedia", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("MainWindow media-selection method was not found.");
        MethodInfo saveMethod = type.GetMethod("SaveProject", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("MainWindow project-save method was not found.");

        if (projectField.GetValue(window) is not ProjectDocument project ||
            statusField.GetValue(window) is not TextBlock statusText)
        {
            throw new InvalidOperationException("MainWindow gallery state could not be accessed.");
        }

        MediaGalleryController? controller = null;
        Action saveAndRefresh = () =>
        {
            saveMethod.Invoke(window, null);
            controller?.Refresh();
        };

        controller = CreateForWindow(
            window,
            () => project.Media,
            asset => selectMethod.Invoke(window, new object[] { asset }),
            saveAndRefresh,
            asset => project.Media.Remove(asset),
            message => statusText.Text = message);
        return controller;
    }

    private static MediaGalleryController CreateForWindow(
        Window window,
        Func<IReadOnlyList<MediaAsset>> getAssets,
        Action<MediaAsset> selectMedia,
        Action saveProject,
        Action<MediaAsset> removeMedia,
        Action<string> setStatus)
    {
        if (window.Content is not Grid root || root.Children.Count < 2 || root.Children[1] is not Border mediaBorder)
        {
            throw new InvalidOperationException("MagmaEdit media panel is not available.");
        }

        if (mediaBorder.Child is not StackPanel mediaPanel)
        {
            throw new InvalidOperationException("MagmaEdit media panel layout is not available.");
        }

        StackPanel? legacyHost = mediaPanel.Children.OfType<StackPanel>().LastOrDefault();
        if (legacyHost is null)
        {
            throw new InvalidOperationException("MagmaEdit media list is not available.");
        }

        int hostIndex = mediaPanel.Children.IndexOf(legacyHost);
        var host = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            ItemWidth = 148,
            ItemHeight = 286
        };

        var scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MaxHeight = 560,
            Content = host
        };

        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "Search:",
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };

        mediaPanel.Children.RemoveAt(hostIndex);
        mediaPanel.Children.Insert(hostIndex, scrollViewer);
        mediaPanel.Children.Insert(hostIndex, controls);

        var gallery = new MediaGalleryController(window, host, getAssets, selectMedia, saveProject, removeMedia, setStatus);
        controls.Children.Add(gallery._searchBox);
        controls.Children.Add(gallery._sortBox);
        controls.Children.Add(gallery._statusFilterBox);
        return gallery;
    }

    public void Refresh()
    {
        if (_disposed || _refreshing)
        {
            return;
        }

        _refreshing = true;
        try
        {
            _host.Children.Clear();
            string search = _searchBox.Text?.Trim() ?? string.Empty;
            bool newestFirst = _sortBox.SelectedIndex != 1;
            int statusFilter = _statusFilterBox.SelectedIndex;

            IEnumerable<MediaAsset> assets = _getAssets()
                .Where(asset => search.Length == 0 || asset.FileName.Contains(search, StringComparison.OrdinalIgnoreCase));

            assets = statusFilter switch
            {
                1 => assets.Where(asset => asset.IsPublished),
                2 => assets.Where(asset => !asset.IsPublished),
                _ => assets
            };

            assets = newestFirst
                ? assets.OrderByDescending(GetSortTimestamp).ThenBy(asset => asset.FileName, StringComparer.OrdinalIgnoreCase)
                : assets.OrderBy(GetSortTimestamp).ThenBy(asset => asset.FileName, StringComparer.OrdinalIgnoreCase);

            foreach (MediaAsset asset in assets)
            {
                _host.Children.Add(CreateCard(asset));
            }

            _setStatus($"Showing {_host.Children.Count} video{(_host.Children.Count == 1 ? string.Empty : "s")}." +
                       (search.Length == 0 ? string.Empty : $" Search: {search}"));
        }
        finally
        {
            _refreshing = false;
        }
    }

    private Border CreateCard(MediaAsset asset)
    {
        var thumbnail = new Image
        {
            Width = 112,
            Height = 199,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Source = _thumbnailCache.TryGetValue(asset.LibraryPath, out Bitmap? cached) ? cached : _placeholderBitmap
        };

        if (!_thumbnailCache.ContainsKey(asset.LibraryPath))
        {
            _ = LoadThumbnailAsync(asset, thumbnail);
        }

        var selectButton = new Button
        {
            Content = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    thumbnail,
                    new TextBlock
                    {
                        Text = asset.FileName,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = 112,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextAlignment = TextAlignment.Center
                    }
                }
            },
            Padding = new Thickness(6),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        ToolTip.SetTip(selectButton, asset.LibraryPath);
        selectButton.Click += (_, _) => _selectMedia(asset);

        var publishButton = new Button
        {
            Content = asset.IsPublished ? "Published" : "Not Published",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        publishButton.Click += (_, _) =>
        {
            asset.IsPublished = !asset.IsPublished;
            _saveProject();
            _setStatus($"{asset.FileName}: {(asset.IsPublished ? "Published" : "Not Published")}");
        };

        var removeButton = new Button
        {
            Content = "Remove from Gallery",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        removeButton.Click += (_, _) => RemoveFromGallery(asset);

        return new Border
        {
            Width = 148,
            Padding = new Thickness(8),
            Margin = new Thickness(4),
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Width = 132,
                Spacing = 6,
                Children = { selectButton, publishButton, removeButton }
            }
        };
    }

    private void RemoveFromGallery(MediaAsset asset)
    {
        if (!_getAssets().Contains(asset))
        {
            _setStatus($"{asset.FileName} is no longer in the gallery.");
            return;
        }

        _removeMedia(asset);
        _saveProject();
        if (_thumbnailCache.Remove(asset.LibraryPath, out Bitmap? bitmap))
        {
            bitmap.Dispose();
        }

        Refresh();
        _setStatus($"Removed {asset.FileName} from the gallery. The Content Creation copy was kept.");
    }

    private async Task LoadThumbnailAsync(MediaAsset asset, Image target)
    {
        try
        {
            DecodedPreviewFrame frame = await MediaPreviewService.DecodeFirstFrameAsync(asset.LibraryPath).ConfigureAwait(false);
            WriteableBitmap bitmap = CreateBitmap(frame);

            if (_disposed)
            {
                bitmap.Dispose();
                return;
            }

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_disposed)
                {
                    bitmap.Dispose();
                    return;
                }

                if (_thumbnailCache.TryGetValue(asset.LibraryPath, out Bitmap? existing))
                {
                    bitmap.Dispose();
                    target.Source = existing;
                    return;
                }

                _thumbnailCache[asset.LibraryPath] = bitmap;
                target.Source = bitmap;
            });
        }
        catch (Exception)
        {
            // Keep the placeholder when a media item cannot produce a thumbnail.
        }
    }

    private static DateTime GetSortTimestamp(MediaAsset asset)
    {
        try
        {
            return File.Exists(asset.LibraryPath)
                ? File.GetCreationTimeUtc(asset.LibraryPath)
                : DateTime.MinValue;
        }
        catch (IOException)
        {
            return DateTime.MinValue;
        }
        catch (UnauthorizedAccessException)
        {
            return DateTime.MinValue;
        }
    }

    private static WriteableBitmap CreateBitmap(DecodedPreviewFrame frame)
    {
        GCHandle handle = GCHandle.Alloc(frame.Pixels, GCHandleType.Pinned);
        try
        {
            return new WriteableBitmap(
                PixelFormats.Rgba8888,
                AlphaFormat.Opaque,
                handle.AddrOfPinnedObject(),
                new PixelSize(frame.Width, frame.Height),
                new Vector(96, 96),
                frame.RowBytes);
        }
        finally
        {
            handle.Free();
        }
    }

    private static WriteableBitmap CreatePlaceholderBitmap()
    {
        const int width = 36;
        const int height = 64;
        byte[] pixels = new byte[width * height * 4];
        for (int index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 28;
            pixels[index + 1] = 28;
            pixels[index + 2] = 28;
            pixels[index + 3] = 255;
        }

        GCHandle handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            return new WriteableBitmap(
                PixelFormats.Rgba8888,
                AlphaFormat.Opaque,
                handle.AddrOfPinnedObject(),
                new PixelSize(width, height),
                new Vector(96, 96),
                width * 4);
        }
        finally
        {
            handle.Free();
        }
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e) => Refresh();

    private void FilterChanged(object? sender, SelectionChangedEventArgs e) => Refresh();

    private void Window_Closed(object? sender, EventArgs e) => Dispose();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _window.Closed -= Window_Closed;
        _searchBox.TextChanged -= SearchBox_TextChanged;
        _sortBox.SelectionChanged -= FilterChanged;
        _statusFilterBox.SelectionChanged -= FilterChanged;

        foreach (Bitmap bitmap in _thumbnailCache.Values)
        {
            bitmap.Dispose();
        }

        _thumbnailCache.Clear();
        _placeholderBitmap.Dispose();
    }
}
