using Fotopodglad.Helpers;
using Fotopodglad.Models;
using Xunit;

namespace Fotopodglad.Tests.Services;

public sealed class MasonryLayoutTests
{
    private static PhotoItem MakePhoto(long sequenceId, double aspectRatio = 1.5)
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
    public void ComputeLayout_PlacesNewestItem_AtTopLeft()
    {
        var newest = MakePhoto(3);

        var (slots, _) = MasonryLayoutCalculator.ComputeLayout(
            new[] { newest, MakePhoto(2), MakePhoto(1) }, columnCount: 3, columnWidth: 120);

        Assert.Equal(new MasonrySlot(0, 0, 120, 80), slots[newest]);
    }

    [Fact]
    public void ComputeLayout_FillsCompleteRow_LeftToRight()
    {
        var items = Enumerable.Range(1, 6).Select(i => MakePhoto(i)).ToArray();

        var (slots, _) = MasonryLayoutCalculator.ComputeLayout(items, columnCount: 6, columnWidth: 100);

        Assert.Equal(new double[] { 0, 100, 200, 300, 400, 500 }, items.Select(item => slots[item].X));
        Assert.All(items, item => Assert.Equal(0, slots[item].Y));
        Assert.All(items, item => Assert.Equal(100.0 / 1.5, slots[item].Height, precision: 5));
    }

    [Fact]
    public void ComputeLayout_StartsNextRowAfterColumnCountItems()
    {
        var items = Enumerable.Range(1, 7).Select(i => MakePhoto(i, aspectRatio: i)).ToArray();

        var (slots, _) = MasonryLayoutCalculator.ComputeLayout(items, columnCount: 6, columnWidth: 120);

        Assert.Equal(new MasonrySlot(0, 80, 120, 80), slots[items[6]]);
    }

    [Fact]
    public void ComputeLayout_UsesSameTileSizeForEveryPhotoAspectRatio()
    {
        var wide = MakePhoto(1, 4.0);
        var portrait = MakePhoto(2, 0.4);

        var (slots, totalHeight) = MasonryLayoutCalculator.ComputeLayout(
            new[] { wide, portrait }, columnCount: 1, columnWidth: 150);

        Assert.Equal(100, slots[wide].Height);
        Assert.Equal(100, slots[portrait].Height);
        Assert.Equal(200, totalHeight);
    }

    [Fact]
    public void ComputeScrollOffsetToReveal_KeepsOffset_WhenTileAlreadyFullyVisible()
    {
        var slot = new MasonrySlot(0, 220, 300, 200);

        Assert.Null(MasonryLayoutCalculator.ComputeScrollOffsetToReveal(slot, verticalOffset: 200, viewportHeight: 600));
    }

    [Fact]
    public void ComputeScrollOffsetToReveal_CentersTileBelowViewport()
    {
        var slot = new MasonrySlot(0, 1000, 300, 200);

        var offset = MasonryLayoutCalculator.ComputeScrollOffsetToReveal(slot, verticalOffset: 0, viewportHeight: 600);

        Assert.Equal(800, offset);
    }

    [Fact]
    public void ComputeScrollOffsetToReveal_DoesNotScrollAboveTop()
    {
        var slot = new MasonrySlot(0, 0, 300, 200);

        var offset = MasonryLayoutCalculator.ComputeScrollOffsetToReveal(slot, verticalOffset: 900, viewportHeight: 600);

        Assert.Equal(0, offset);
    }

    [Fact]
    public void ComputeScrollOffsetToReveal_IgnoresUnmeasuredViewport()
    {
        var slot = new MasonrySlot(0, 1000, 300, 200);

        Assert.Null(MasonryLayoutCalculator.ComputeScrollOffsetToReveal(slot, verticalOffset: 0, viewportHeight: 0));
    }

    [Fact]
    public void ComputeLayout_HandlesFifteenThousandPhotos()
    {
        var items = Enumerable.Range(1, 15_000).Select(i => MakePhoto(i)).ToArray();

        var (slots, totalHeight) = MasonryLayoutCalculator.ComputeLayout(items, 8, 100);

        Assert.Equal(15_000, slots.Count);
        Assert.Equal(Math.Ceiling(15_000d / 8) * (100d / 1.5), totalHeight, precision: 5);
    }
}
