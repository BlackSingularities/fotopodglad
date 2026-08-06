using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Fotopodglad.Helpers;
using Fotopodglad.Models;
using Fotopodglad.Services;

namespace Fotopodglad.Controls;

/// <summary>
/// Regularna siatka równych kafelków 3:2. Zdjęcia są układane rzędami od lewej do prawej,
/// przycinane do kafelków i przewijane pionowo. Własna wirtualizacja tworzy kontrolki Image
/// tylko dla bieżącego viewportu i bufora, więc duże foldery nie obciążają niepotrzebnie UI.
/// </summary>
public partial class MasonryGridControl : UserControl
{
    private const double ViewportBufferRatio = 1.0; // dodatkowy bufor nad/pod viewportem, w wysokościach ekranu

    private int _columnCount = 6;

    private readonly Dictionary<PhotoItem, Image> _realizedImages = new();
    private readonly Stack<Image> _imagePool = new();
    private readonly Dictionary<PhotoItem, CancellationTokenSource> _thumbnailLoads = new();
    private Dictionary<PhotoItem, MasonrySlot> _layout = new();
    private IThumbnailCache? _thumbnailCache;
    private ObservableCollection<PhotoItem>? _itemsSource;

    public event Action<PhotoItem>? PhotoClicked;
    public event Action<PhotoItem>? PhotoFlagToggled;

    public MasonryGridControl()
    {
        InitializeComponent();
        SizeChanged += (_, _) => RecomputeLayout();
        Loaded += (_, _) => RecomputeLayout();
    }

    public void Initialize(IThumbnailCache thumbnailCache, ObservableCollection<PhotoItem> itemsSource, int columnCount = 6)
    {
        _thumbnailCache = thumbnailCache;
        _columnCount = Math.Max(1, columnCount);

        if (_itemsSource is not null)
        {
            _itemsSource.CollectionChanged -= OnItemsSourceChanged;
        }

        _itemsSource = itemsSource;
        _itemsSource.CollectionChanged += OnItemsSourceChanged;
        RecomputeLayout();
    }

    public void SetColumnCount(int columnCount)
    {
        var clamped = Math.Clamp(columnCount, 2, 12);
        if (_columnCount == clamped)
        {
            return;
        }

        _columnCount = clamped;
        RecomputeLayout();
    }

    private void OnItemsSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => RecomputeLayout();

    private void RecomputeLayout()
    {
        if (_itemsSource is null || ActualWidth <= 0)
        {
            return;
        }

        var wasAtTop = Scroller.VerticalOffset <= 5;
        var previousOffset = Scroller.VerticalOffset;

        var columnWidth = ActualWidth / _columnCount;
        var (slots, totalHeight) = MasonryLayoutCalculator.ComputeLayout(_itemsSource, _columnCount, columnWidth);
        _layout = slots;

        LayoutCanvas.Width = ActualWidth;
        LayoutCanvas.Height = totalHeight;

        // Kolekcja jest uzupełniana asynchronicznie podczas skanu folderu. Po każdym wstawieniu
        // zmieniają się indeksy wszystkich późniejszych zdjęć, więc już istniejące kontrolki również
        // muszą dostać nowe współrzędne — samo przeliczenie słownika slotów nie wystarcza.
        foreach (var (item, image) in _realizedImages)
        {
            if (_layout.TryGetValue(item, out var slot))
            {
                ApplySlot(image, slot);
            }
        }

        if (wasAtTop)
        {
            Scroller.ScrollToVerticalOffset(0);
        }
        else
        {
            Scroller.ScrollToVerticalOffset(previousOffset);
        }

        UpdateVirtualization();
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e) => UpdateVirtualization();

    private void UpdateVirtualization()
    {
        if (_thumbnailCache is null || _itemsSource is null)
        {
            return;
        }

        var viewportHeight = Scroller.ViewportHeight;
        if (viewportHeight <= 0)
        {
            return;
        }

        var buffer = viewportHeight * ViewportBufferRatio;
        var rangeTop = Scroller.VerticalOffset - buffer;
        var rangeBottom = Scroller.VerticalOffset + viewportHeight + buffer;

        var toRemove = new List<PhotoItem>();
        foreach (var (item, _) in _realizedImages)
        {
            if (!_layout.TryGetValue(item, out var slot) || slot.Y + slot.Height < rangeTop || slot.Y > rangeBottom)
            {
                toRemove.Add(item);
            }
        }

        foreach (var item in toRemove)
        {
            DerealizeItem(item);
        }

        var tileHeight = ActualWidth / _columnCount / 1.5;
        var firstRow = Math.Max(0, (int)Math.Floor(rangeTop / tileHeight));
        var lastRow = Math.Max(firstRow, (int)Math.Ceiling(rangeBottom / tileHeight));
        var firstIndex = firstRow * _columnCount;
        var itemCount = Math.Min(_itemsSource.Count - firstIndex, (lastRow - firstRow + 1) * _columnCount);
        if (itemCount <= 0)
        {
            return;
        }

        foreach (var item in _itemsSource.Skip(firstIndex).Take(itemCount))
        {
            if (_realizedImages.ContainsKey(item))
            {
                continue;
            }

            if (!_layout.TryGetValue(item, out var slot))
            {
                continue;
            }

            if (slot.Y + slot.Height < rangeTop || slot.Y > rangeBottom)
            {
                continue;
            }

            RealizeItem(item, slot);
        }
    }

    private void RealizeItem(PhotoItem item, MasonrySlot slot)
    {
        var image = _imagePool.Count > 0 ? _imagePool.Pop() : CreateImage();

        ApplySlot(image, slot);
        image.Tag = item;
        image.Source = null;

        LayoutCanvas.Children.Add(image);
        _realizedImages[item] = image;

        var decodeWidth = Math.Max(1, (int)(slot.Width * 1.5));
        var cancellation = new CancellationTokenSource();
        _thumbnailLoads[item] = cancellation;
        _ = LoadThumbnailAsync(item, image, decodeWidth, cancellation.Token);
    }

    private static void ApplySlot(Image image, MasonrySlot slot)
    {
        Canvas.SetLeft(image, slot.X);
        Canvas.SetTop(image, slot.Y);
        image.Width = slot.Width;
        image.Height = slot.Height;
    }

    private async Task LoadThumbnailAsync(PhotoItem item, Image target, int decodeWidth, CancellationToken cancellationToken)
    {
        if (_thumbnailCache is null)
        {
            return;
        }

        BitmapImage? bitmap;
        try
        {
            bitmap = await _thumbnailCache.GetThumbnailAsync(item.FilePath, decodeWidth, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // Kontrolka mogła zostać w międzyczasie zrecyklowana do innego zdjęcia — nie nadpisuj cudzego obrazu.
        if (ReferenceEquals(target.Tag, item) && bitmap is not null)
        {
            target.Source = bitmap;
        }
    }

    private void DerealizeItem(PhotoItem item)
    {
        if (!_realizedImages.Remove(item, out var image))
        {
            return;
        }

        if (_thumbnailLoads.Remove(item, out var cancellation))
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }

        image.Tag = null;
        image.Source = null;
        LayoutCanvas.Children.Remove(image);
        _imagePool.Push(image);
    }

    private Image CreateImage()
    {
        var image = new Image
        {
            Stretch = Stretch.UniformToFill,
            ClipToBounds = true,
            SnapsToDevicePixels = true
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        image.MouseLeftButtonUp += OnImageClicked;
        image.MouseRightButtonUp += OnImageRightClicked;
        return image;
    }

    private void OnImageClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is Image { Tag: PhotoItem item })
        {
            PhotoClicked?.Invoke(item);
        }
    }

    private void OnImageRightClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is Image { Tag: PhotoItem item })
        {
            PhotoFlagToggled?.Invoke(item);
            e.Handled = true;
        }
    }
}
