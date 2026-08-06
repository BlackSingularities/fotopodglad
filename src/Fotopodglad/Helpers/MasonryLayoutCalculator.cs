using Fotopodglad.Models;

namespace Fotopodglad.Helpers;

public readonly record struct MasonrySlot(double X, double Y, double Width, double Height);

/// <summary>
/// Czysta, testowalna implementacja algorytmu masonry używanego przez MasonryGridControl:
/// iterując zdjęcia w kolejności od najnowszego (indeks 0), każde trafia do aktualnie
/// najkrótszej kolumny — dzięki temu najnowsze zawsze ląduje najbliżej Y=0.
/// </summary>
public static class MasonryLayoutCalculator
{
    public static (Dictionary<PhotoItem, MasonrySlot> Slots, double TotalHeight) ComputeLayout(
        IEnumerable<PhotoItem> itemsNewestFirst, int columnCount, double columnWidth)
    {
        var slots = new Dictionary<PhotoItem, MasonrySlot>();
        var columnHeights = new double[columnCount];

        foreach (var item in itemsNewestFirst)
        {
            var shortestColumn = 0;
            var shortestHeight = columnHeights[0];
            for (var c = 1; c < columnCount; c++)
            {
                if (columnHeights[c] < shortestHeight)
                {
                    shortestHeight = columnHeights[c];
                    shortestColumn = c;
                }
            }

            var aspect = item.Exif.AspectRatio is > 0 ? item.Exif.AspectRatio : 1.5;
            var tileHeight = columnWidth / aspect;

            slots[item] = new MasonrySlot(shortestColumn * columnWidth, columnHeights[shortestColumn], columnWidth, tileHeight);
            columnHeights[shortestColumn] += tileHeight;
        }

        var totalHeight = columnHeights.Length > 0 ? columnHeights.Max() : 0;
        return (slots, totalHeight);
    }
}
