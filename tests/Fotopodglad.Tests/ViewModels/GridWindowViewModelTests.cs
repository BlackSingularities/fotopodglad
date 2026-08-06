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
    public void PhotoClick_OpensSelectedPhotoInMainPreview()
    {
        var library = new FakePhotoLibrary();
        var photo = new PhotoItem
        {
            FilePath = Path.Combine(Path.GetTempPath(), "missing-test-photo.jpg"),
            FileName = "test.jpg",
            Exif = new ExifData(),
            DiscoveredAtUtc = DateTime.UtcNow,
            SequenceId = 1
        };
        library.Photos.Add(photo);

        var preview = new FullscreenPhotoViewModel(library, new AppSettings());
        var mainView = new MainViewWindowViewModel(preview);
        var viewModel = new GridWindowViewModel(library, mainView);

        viewModel.OnPhotoClicked(photo);

        Assert.Same(photo, mainView.Preview.CurrentPhoto);
        Assert.Equal(PreviewMode.Manual, mainView.Preview.Mode);
    }

    private sealed class FakePhotoLibrary : IPhotoLibraryService
    {
        public ObservableCollection<PhotoItem> Photos { get; } = new();
        public PhotoItem? Latest => Photos.FirstOrDefault();
        public event Action<PhotoItem>? NewestChanged
        {
            add { }
            remove { }
        }

        public void Start(string folderPath)
        {
        }

        public void Stop()
        {
        }
    }
}
