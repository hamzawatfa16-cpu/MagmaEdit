using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using MagmaEdit.Core.Media;

namespace MagmaEdit.App;

/// <summary>Owns the video-library gallery controls, ordering, search, thumbnails, and publish state UI.</summary>
public sealed class MediaGalleryController : IDisposable
{
    private readonly StackPanel _host;
    private readonly Func<IReadOnlyList<MediaAsset>> _getAssets;
    private readonly Action<MediaAsset> _selectMedia;
    private readonly Action _saveProject;
    private readonly Action<string> _setStatus;
    private readonly TextBox _searchBox;
    private readonly ComboBox _sortBox;
    private readonly StackPanel _grid;
    private readonly Dictionary<string, Bitmap> _thumbnailCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public MediaGalleryController(
        StackPanel host,
        Func<IReadOnlyList<MediaAsset>> getAssets,
        Action<MediaAsset> selectMedia,
        Action saveProject,
        Action<string> setStatus)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _getAssets = getAssets ?? throw new ArgumentNullException(nameof(getAssets));
        _selectMedia = selectMedia ?? throw new ArgumentNullException(nameof(selectMedia));
        _saveProject = saveProject ?? throw new ArgumentNullException(nameof(saveProject));
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));

        _searchBox = new TextBox
        {
            Watermark = "Search videos by name…",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _searchBox.TextChanged += SearchBox_TextChanged;

        _sortBox = new ComboBox
        {
            SelectedIndex = 0,
            ItemsSource = new[] { "Newest", "Oldest" },
            MinWidth = 110
        };
        _sortBox.SelectionChanged += SortBox_SelectionChanged;

        _grid = new StackPanel { Spacing = 8 };
        _host.Children.Clear();
        _host.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { _searchBox, _sortBox }
        });
        _host.Children.Add(_grid);
    }

    public void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        _grid.Children.Clear();
        string search = _searchBox.Text?.Trim() ?? string.Empty;
        bool newestFirst = _sortBox.SelectedIndex != 1;

        IEnumerable<MediaAsset> assets = _getAssets()
            .Where(asset => search.Length == 0 || asset.FileName.Contains(search, StringComparison.OrdinalIgnoreCase));

        assets = newestFirst
            ? assets.OrderByDescending(GetSortTimestamp).ThenBy(asset => asset.FileName, StringComparer.OrdinalIgnoreCase)
            : assets.OrderBy(GetSortTimestamp).ThenBy(asset => asset.FileName, StringComparer.OrdinalIgnoreCase);

        foreach (MediaAsset asset in assets)
        {
            _grid.Children.Add(CreateCard(asset));
        }

        _setStatus($"Showing {_grid.Children.Count} video{(_grid.Children.Count == 1 ? string.Empty : "s")}." +
                   (search.Length == 0 ? string.Empty : $" Search: {search}"));
    }

    private Control CreateCard(MediaAsset asset)
    {
        var thumbnail = new Image
        {
            Width = 112,
            Height = 199,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Source = CreatePlaceholderBitmap()
        };

        if (_thumbnailCache.TryGetValue(asset.LibraryPath, out Bitmap? cached))
        {
            thumbnail.Source = cached;
        }
        else
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
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }
            },
            Padding = new Thickness(6),
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        selectButton.Click += (_, _) => _selectMedia(asset);

        var publishButton = new Button
        {
            Content = asset.IsPublished ? "Published" : "Not Published",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        publishButton.Click += (_, _) =>
        {
            asset.IsPublished = !asset.IsPublished;
            publishButton.Content = asset.IsPublished ? "Published" : "Not Published";
            _saveProject();
            _setStatus($"{asset.FileName}: {(asset.IsPublished ? "Published" : "Not Published")}");
        };

        return new Border
        {
            Padding = new Thickness(8),
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Width = 136,
                Spacing = 6,
                Children = { selectButton, publishButton }
            }
        };
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

            Bitmap? previous = null;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_disposed)
                {
                    bitmap.Dispose();
                    return;
                }

                if (_thumbnailCache.TryGetValue(asset.LibraryPath, out Bitmap? existing))
                {
                    previous = existing;
                }
                else
                {
                    _thumbnailCache[asset.LibraryPath] = bitmap;
                }

                target.Source = _thumbnailCache[asset.LibraryPath];
            });

            if (previous is not null)
            {
                bitmap.Dispose();
            }
        }
        catch (Exception) when (true)
        {
            // Gallery placeholders remain visible when a thumbnail cannot be decoded.
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

    private void SortBox_SelectionChanged(object? sender, SelectionChangedEventArgs e) => Refresh();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _searchBox.TextChanged -= SearchBox_TextChanged;
        _sortBox.SelectionChanged -= SortBox_SelectionChanged;
        foreach (Bitmap bitmap in _thumbnailCache.Values)
        {
            bitmap.Dispose();
        }

        _thumbnailCache.Clear();
    }
}
