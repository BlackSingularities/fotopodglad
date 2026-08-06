using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Fotopodglad.Helpers;
using Fotopodglad.Models;
using Fotopodglad.Services;

namespace Fotopodglad.Controls;

/// <summary>
/// Niekończąca się siatka masonry o 6 kolumnach: zdjęcia pakowane pionowo bez odstępów, kolejne zawsze
/// trafiają do aktualnie najkrótszej kolumny. Zdjęcia w ItemsSource są posortowane malejąco po czasie
/// (najnowsze na indeksie 0), więc iterując w tej kolejności najnowsze zawsze ląduje najbliżej Y=0.
/// Wirtualizacja własna: tylko elementy w viewport + bufor dostają realny wizual (Image), reszta to
/// sam wyliczony layout — potrzebne, bo układ masonry nie jest wspierany przez VirtualizingStackPanel.
/// </summary>
public partial class MasonryGridControl : UserControl
{
    private const double ViewportBufferRatio = 1.0; // dodatkowy bufor nad/pod viewportem, w wysokościach ekranu

    private int _columnCount = 6;

    private readonly Dictionary<PhotoItem, Image> _realizedImages = new();
    private readonly Stack<Image> _imagePool = new();
    private Dictionary<PhotoItem, MasonrySlot> _layout = new();
    private IThumbnailCache? _thumbnailCache;
    private ObservableCollection<PhotoItem>? _itemsSource;

    public event Action<PhotoItem>? PhotoClicked;

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

        foreach (var item in _itemsSource)
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

        Canvas.SetLeft(image, slot.X);
        Canvas.SetTop(image, slot.Y);
        image.Width = slot.Width;
        image.Height = slot.Height;
        image.Tag = item;
        image.Source = null;

        LayoutCanvas.Children.Add(image);
        _realizedImages[item] = image;

        var decodeWidth = Math.Max(1, (int)(slot.Width * 1.5));
        _ = LoadThumbnailAsync(item, image, decodeWidth);
    }

    private async Task LoadThumbnailAsync(PhotoItem item, Image target, int decodeWidth)
    {
        if (_thumbnailCache is null)
        {
            return;
        }

        var bitmap = await _thumbnailCache.GetThumbnailAsync(item.FilePath, decodeWidth);

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
        return image;
    }

    private void OnImageClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is Image { Tag: PhotoItem item })
        {
            PhotoClicked?.Invoke(item);
        }
    }
}
