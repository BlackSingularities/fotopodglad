using System.Collections.ObjectModel;
using Fotopodglad.Configuration;
using Fotopodglad.Models;
using Fotopodglad.Services;
using Fotopodglad.ViewModels;
using Xunit;

namespace Fotopodglad.Tests.ViewModels;

public sealed class GridWindowViewModelTests
{
    [Fact]
    public void DefaultPreview_DoesNotSelectLatestPhotoAutomatically()
    {
        var library = new FakePhotoLibrary();
        library.Add(NewPhoto("latest.jpg", 1));

        var preview = new FullscreenPhotoViewModel(library, new AppSettings());
        _ = new MainViewWindowViewModel(preview);

        Assert.Null(preview.CurrentPhoto);
        Assert.Equal(PreviewMode.Manual, preview.Mode);
    }

    [Fact]
    public void AutomaticPreview_ShowsAndFollowsLatestPhotoWhenEnabled()
    {
        var library = new FakePhotoLibrary();
        var latest = NewPhoto("latest.jpg", 2);
        var preview = new FullscreenPhotoViewModel(library, new AppSettings { AutomaticallyShowLatestPhoto = true });

        library.Add(latest);

        Assert.Same(latest, preview.CurrentPhoto);
        Assert.Equal(PreviewMode.Auto, preview.Mode);
    }

    [Fact]
    public void EnablingAutomaticPreview_StartsFollowingLatestPhotoWithoutRestart()
    {
        var library = new FakePhotoLibrary();
        var latest = NewPhoto("latest.jpg", 1);
        library.Add(latest);
        var settings = new AppSettings();
        var preview = new FullscreenPhotoViewModel(library, settings);

        settings.AutomaticallyShowLatestPhoto = true;
        settings.NotifyChanged();

        Assert.Same(latest, preview.CurrentPhoto);
        Assert.Equal(PreviewMode.Auto, preview.Mode);
    }

    [Fact]
    public void AutomaticPreview_KeepsManualSelectionWhenNewPhotoArrivesDuringHoldTime()
    {
        var library = new FakePhotoLibrary();
        var selected = NewPhoto("selected.jpg", 1);
        library.Add(selected);
        var preview = new FullscreenPhotoViewModel(library, new AppSettings
        {
            AutomaticallyShowLatestPhoto = true,
            ManualHoldSeconds = AppSettings.MinManualHoldSeconds
        });

        preview.ShowManually(selected);
        library.Add(NewPhoto("new.jpg", 2));

        Assert.Same(selected, preview.CurrentPhoto);
        Assert.Equal(PreviewMode.Manual, preview.Mode);
    }

    [Fact]
    public void PhotoClick_OpensSelectedPhotoInMainPreview()
    {
        var library = new FakePhotoLibrary();
        var photo = NewPhoto("test.jpg", 1);
        library.Add(photo);

        var preview = new FullscreenPhotoViewModel(library, new AppSettings());
        var mainView = new MainViewWindowViewModel(preview);
        var viewModel = new GridWindowViewModel(library, mainView);

        viewModel.OnPhotoClicked(photo);

        Assert.Same(photo, mainView.Preview.CurrentPhoto);
        Assert.Equal(PreviewMode.Manual, mainView.Preview.Mode);
    }

    [Fact]
    public void Arrows_EnterTheGalleryFromEmptyPreview()
    {
        var library = new FakePhotoLibrary();
        library.Add(NewPhoto("older.jpg", 1));
        var latest = NewPhoto("latest.jpg", 2);
        library.Add(latest);
        var preview = new FullscreenPhotoViewModel(library, new AppSettings());

        // Bez automatycznego podglądu okno startuje puste — pierwsza strzałka musi coś pokazać.
        preview.ShowPrevious();

        Assert.Same(latest, preview.CurrentPhoto);
        Assert.Equal(PreviewMode.Manual, preview.Mode);
    }

    [Fact]
    public void Arrows_WalkFromNewestToOldestAndBack()
    {
        var library = new FakePhotoLibrary();
        var oldest = NewPhoto("oldest.jpg", 1);
        var middle = NewPhoto("middle.jpg", 2);
        var newest = NewPhoto("newest.jpg", 3);
        library.Add(oldest);
        library.Add(middle);
        library.Add(newest);
        var preview = new FullscreenPhotoViewModel(library, new AppSettings());
        preview.ShowManually(newest);

        preview.ShowPrevious();
        Assert.Same(middle, preview.CurrentPhoto);

        preview.ShowPrevious();
        Assert.Same(oldest, preview.CurrentPhoto);

        preview.ShowPrevious();
        Assert.Same(oldest, preview.CurrentPhoto); // koniec listy — bez zawijania

        preview.ShowNext();
        Assert.Same(middle, preview.CurrentPhoto);
    }

    [Fact]
    public void Arrows_SkipPhotosHiddenByGalleryFilter()
    {
        var library = new FakePhotoLibrary();
        var oldestFlagged = NewPhoto("flagged-old.jpg", 1);
        var hidden = NewPhoto("hidden.jpg", 2);
        var newestFlagged = NewPhoto("flagged-new.jpg", 3);
        oldestFlagged.IsFlagged = true;
        newestFlagged.IsFlagged = true;
        library.Add(oldestFlagged);
        library.Add(hidden);
        library.Add(newestFlagged);

        var settings = new AppSettings { PhotoFilter = PhotoFilterMode.Flagged };
        var preview = new FullscreenPhotoViewModel(library, settings);
        _ = new GridWindowViewModel(library, new MainViewWindowViewModel(preview), settings);
        preview.ShowManually(newestFlagged);

        preview.ShowPrevious();

        Assert.Same(oldestFlagged, preview.CurrentPhoto);
    }

    private static PhotoItem NewPhoto(string fileName, long sequenceId) => new()
    {
        FilePath = Path.Combine(Path.GetTempPath(), $"missing-{fileName}"),
        FileName = fileName,
        Exif = new ExifData(),
        DiscoveredAtUtc = DateTime.UtcNow,
        SequenceId = sequenceId
    };

    private sealed class FakePhotoLibrary : IPhotoLibraryService
    {
        public ObservableCollection<PhotoItem> Photos { get; } = new();
        public PhotoItem? Latest => Photos.FirstOrDefault();
        public event Action<PhotoItem>? NewestChanged;

        public void Add(PhotoItem photo)
        {
            Photos.Insert(0, photo);
            NewestChanged?.Invoke(photo);
        }

        public void Start(string folderPath)
        {
        }

        public void Stop()
        {
        }
    }
}
