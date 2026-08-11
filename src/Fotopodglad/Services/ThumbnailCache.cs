using System.Windows.Media.Imaging;
using Fotopodglad.Helpers;
using Fotopodglad.Configuration;

namespace Fotopodglad.Services;

/// <summary>
/// Cache miniatur w pamięci (LRU) używany przez siatkę zdjęć, żeby uniknąć powtórnego dekodowania
/// tej samej bitmapy przy każdym przewinięciu widoku (recykling kontrolek w MasonryGridControl).
/// Dekodowanie odbywa się na wątku tła; bitmapy są zamrożone (Freeze), więc wynik można bezpiecznie
/// przekazać z powrotem na wątek UI.
/// </summary>
public sealed class ThumbnailCache : IThumbnailCache
{
    private readonly SemaphoreSlim _decodeSlots = new(4, 4);
    private readonly Dictionary<string, LinkedListNode<CacheEntry>> _map = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<CacheEntry> _lruOrder = new();
    private readonly object _lock = new();
    private readonly AppSettings _settings;
    private long _currentBytes;

    public ThumbnailCache(AppSettings? settings = null)
    {
        _settings = settings ?? new AppSettings();
        _settings.Changed += (_, _) =>
        {
            lock (_lock)
            {
                EvictIfNeeded();
            }
        };
    }

    public async Task<BitmapSource?> GetThumbnailAsync(string filePath, int decodePixelWidth, CancellationToken cancellationToken = default)
    {
        var key = $"{filePath}|{decodePixelWidth}";

        lock (_lock)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _lruOrder.Remove(node);
                _lruOrder.AddFirst(node);
                return node.Value.Bitmap;
            }
        }

        await _decodeSlots.WaitAsync(cancellationToken);
        BitmapSource? bitmap;
        try
        {
            bitmap = await Task.Run(
                () => BitmapHelper.LoadEmbeddedThumbnailFrozen(filePath, decodePixelWidth, cancellationToken) ??
                      BitmapHelper.LoadFrozen(filePath, decodePixelWidth, cancellationToken), cancellationToken);
        }
        finally
        {
            _decodeSlots.Release();
        }
        if (bitmap is null)
        {
            return null;
        }

        lock (_lock)
        {
            if (!_map.ContainsKey(key))
            {
                var estimatedBytes = (long)bitmap.PixelWidth * bitmap.PixelHeight * 4;
                var node = new LinkedListNode<CacheEntry>(new CacheEntry(key, bitmap, estimatedBytes));
                _lruOrder.AddFirst(node);
                _map[key] = node;
                _currentBytes += estimatedBytes;
                EvictIfNeeded();
            }
        }

        return bitmap;
    }

    private void EvictIfNeeded()
    {
        var maxBytes = Math.Clamp(_settings.ThumbnailCacheMegabytes, 64, 2048) * 1024L * 1024L;
        while (_currentBytes > maxBytes)
        {
            var last = _lruOrder.Last;
            if (last is null)
            {
                break;
            }

            _lruOrder.RemoveLast();
            _map.Remove(last.Value.Key);
            _currentBytes -= last.Value.EstimatedBytes;
        }
    }

    private readonly record struct CacheEntry(string Key, BitmapSource Bitmap, long EstimatedBytes);
}
