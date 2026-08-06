using Fotopodglad.Models;

namespace Fotopodglad.Helpers;

public readonly record struct MasonrySlot(double X, double Y, double Width, double Height);

/// <summary>
/// Czysta, testowalna implementacja regularnej siatki używanej przez MasonryGridControl.
/// Wszystkie kafelki mają identyczny rozmiar 3:2 i tworzą pełne rzędy od lewej do prawej.
/// Zdjęcia o innych proporcjach są przycinane przez Image.Stretch=UniformToFill.
/// </summary>
public static class MasonryLayoutCalculator
{
    public static (Dictionary<PhotoItem, MasonrySlot> Slots, double TotalHeight) ComputeLayout(
        IEnumerable<PhotoItem> itemsNewestFirst, int columnCount, double columnWidth)
    {
        if (columnCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(columnCount));
        }

        const double tileAspectRatio = 3.0 / 2.0;
        var tileHeight = columnWidth / tileAspectRatio;
        var items = itemsNewestFirst.ToList();
        var slots = new Dictionary<PhotoItem, MasonrySlot>(items.Count);

        for (var index = 0; index < items.Count; index++)
        {
            var column = index % columnCount;
            var row = index / columnCount;
            slots[items[index]] = new MasonrySlot(column * columnWidth, row * tileHeight, columnWidth, tileHeight);
        }

        var rowCount = (int)Math.Ceiling((double)items.Count / columnCount);
        var totalHeight = rowCount * tileHeight;
        return (slots, totalHeight);
    }
}
