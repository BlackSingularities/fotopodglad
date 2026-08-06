using System.Windows.Media.Imaging;
using Fotopodglad.Helpers;

namespace Fotopodglad.Services;

/// <summary>
/// Cache miniatur w pamięci (LRU) używany przez siatkę zdjęć, żeby uniknąć powtórnego dekodowania
/// tej samej bitmapy przy każdym przewinięciu widoku (recykling kontrolek w MasonryGridControl).
/// Dekodowanie odbywa się na wątku tła; bitmapy są zamrożone (Freeze), więc wynik można bezpiecznie
/// przekazać z powrotem na wątek UI.
/// </summary>
public sealed class ThumbnailCache : IThumbnailCache
{
    private const int MaxEntries = 400;

    private readonly Dictionary<string, LinkedListNode<CacheEntry>> _map = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<CacheEntry> _lruOrder = new();
    private readonly object _lock = new();

    public async Task<BitmapImage?> GetThumbnailAsync(string filePath, int decodePixelWidth, CancellationToken cancellationToken = default)
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

        var bitmap = await Task.Run(() => BitmapHelper.LoadFrozen(filePath, decodePixelWidth), cancellationToken);
        if (bitmap is null)
        {
            return null;
        }

        lock (_lock)
        {
            if (!_map.ContainsKey(key))
            {
                var node = new LinkedListNode<CacheEntry>(new CacheEntry(key, bitmap));
                _lruOrder.AddFirst(node);
                _map[key] = node;
                EvictIfNeeded();
            }
        }

        return bitmap;
    }

    private void EvictIfNeeded()
    {
        while (_lruOrder.Count > MaxEntries)
        {
            var last = _lruOrder.Last;
            if (last is null)
            {
                break;
            }

            _lruOrder.RemoveLast();
            _map.Remove(last.Value.Key);
        }
    }

    private readonly record struct CacheEntry(string Key, BitmapImage Bitmap);
}
