using Fotopodglad.Helpers;
using Fotopodglad.Models;
using Xunit;

namespace Fotopodglad.Tests.Services;

public sealed class MasonryLayoutTests
{
    private static PhotoItem MakePhoto(long sequenceId, double aspectRatio)
    {
        var width = 1000;
        var height = (int)(width / aspectRatio);
        return new PhotoItem
        {
            FilePath = $"C:\\photos\\{sequenceId}.jpg",
            FileName = $"{sequenceId}.jpg",
            DiscoveredAtUtc = DateTime.UtcNow,
            SequenceId = sequenceId,
            Exif = ExifData.Empty(width, height, 1024)
        };
    }

    [Fact]
    public void ComputeLayout_PlacesNewestItem_AtTopOfFirstColumn()
    {
        var newest = MakePhoto(3, 1.5);
        var items = new[] { newest, MakePhoto(2, 1.5), MakePhoto(1, 1.5) };

        var (slots, _) = MasonryLayoutCalculator.ComputeLayout(items, columnCount: 6, columnWidth: 100);

        var newestSlot = slots[newest];
        Assert.Equal(0, newestSlot.X);
        Assert.Equal(0, newestSlot.Y);
    }

    [Fact]
    public void ComputeLayout_FillsColumnsLeftToRight_BeforeStacking()
    {
        var items = Enumerable.Range(1, 6).Select(i => MakePhoto(i, 1.0)).ToArray();

        var (slots, _) = MasonryLayoutCalculator.ComputeLayout(items, columnCount: 6, columnWidth: 100);

        // Wszystkie kolumny mają na starcie wysokość 0 — 6 kolejnych zdjęć powinno trafić
        // do 6 różnych kolumn (0..5), każde z Y=0, zanim jakakolwiek kolumna zacznie się piętrzyć.
        var usedColumns = slots.Values.Select(s => s.X).Distinct().OrderBy(x => x).ToArray();
        Assert.Equal(new double[] { 0, 100, 200, 300, 400, 500 }, usedColumns);
        Assert.All(slots.Values, s => Assert.Equal(0, s.Y));
    }

    [Fact]
    public void ComputeLayout_AddsNextItem_ToShortestColumn()
    {
        // Zdjęcie 1: szerokie (mała wysokość kafelka), trafia do kolumny 0.
        // Zdjęcie 2: wysokie (duża wysokość kafelka), trafia do kolumny 1.
        // Zdjęcie 3 powinno trafić do kolumny 0, bo po pierwszych dwóch jest krótsza.
        var wide = MakePhoto(3, 3.0);   // tileHeight = 100/3 ≈ 33.3 -> kolumna 0
        var tall = MakePhoto(2, 0.5);   // tileHeight = 100/0.5 = 200 -> kolumna 1
        var third = MakePhoto(1, 1.0);  // tileHeight = 100

        var (slots, _) = MasonryLayoutCalculator.ComputeLayout(
            new[] { wide, tall, third }, columnCount: 2, columnWidth: 100);

        Assert.Equal(0, slots[wide].X);
        Assert.Equal(100, slots[tall].X);
        Assert.Equal(0, slots[third].X); // krótsza kolumna po dwóch pierwszych elementach
        Assert.Equal(slots[wide].Height, slots[third].Y, precision: 5);
    }

    [Fact]
    public void ComputeLayout_TotalHeight_EqualsTallestColumn()
    {
        var items = new[] { MakePhoto(1, 1.0), MakePhoto(2, 1.0) };

        var (_, totalHeight) = MasonryLayoutCalculator.ComputeLayout(items, columnCount: 6, columnWidth: 120);

        Assert.Equal(120, totalHeight, precision: 5); // jeden element na kolumnę, wysokość = szerokość (aspect 1.0)
    }
}
