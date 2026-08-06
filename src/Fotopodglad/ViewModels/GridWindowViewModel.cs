using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Fotopodglad.Configuration;
using Fotopodglad.Helpers;
using Fotopodglad.Models;
using Fotopodglad.Services;

namespace Fotopodglad.ViewModels;

public sealed partial class GridWindowViewModel : ViewModelBase
{
    private readonly IPhotoLibraryService _library;
    private readonly MainViewWindowViewModel _mainView;
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _filterThrottle;

    public BulkObservableCollection<PhotoItem> Photos { get; } = new();

    [ObservableProperty] private bool isFolderAvailable = true;
    [ObservableProperty] private string folderAvailabilityMessage = string.Empty;

    public GridWindowViewModel(
        IPhotoLibraryService library,
        MainViewWindowViewModel mainView,
        AppSettings? settings = null)
    {
        _library = library;
        _mainView = mainView;
        _settings = settings ?? new AppSettings();
        isFolderAvailable = library.IsFolderAvailable;
        folderAvailabilityMessage = library.FolderAvailabilityMessage ?? string.Empty;
        _library.FolderAvailabilityChanged += OnFolderAvailabilityChanged;
        _library.Photos.CollectionChanged += OnLibraryChanged;
        _settings.Changed += OnSettingsChanged;

        _filterThrottle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _filterThrottle.Tick += (_, _) =>
        {
            _filterThrottle.Stop();
            RebuildFilter();
        };
        RebuildFilter();
    }

    public void OnPhotoClicked(PhotoItem photo) => _mainView.Preview.ShowManually(photo);

    public void ToggleFlag(PhotoItem photo)
    {
        photo.IsFlagged = !photo.IsFlagged;
        if (photo.IsFlagged)
        {
            if (!_settings.FlaggedPhotoPaths.Contains(photo.FilePath, StringComparer.OrdinalIgnoreCase))
            {
                _settings.FlaggedPhotoPaths.Add(photo.FilePath);
            }
        }
        else
        {
            _settings.FlaggedPhotoPaths.RemoveAll(path =>
                string.Equals(path, photo.FilePath, StringComparison.OrdinalIgnoreCase));
        }

        _settings.Save();
        if (_settings.PhotoFilter == PhotoFilterMode.Flagged)
        {
            ScheduleFilter();
        }
    }

    private void OnLibraryChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScheduleFilter();
    private void OnSettingsChanged(object? sender, EventArgs e) => ScheduleFilter();

    private void ScheduleFilter()
    {
        _filterThrottle.Stop();
        _filterThrottle.Start();
    }

    private void RebuildFilter()
    {
        IEnumerable<PhotoItem> filtered = _library.Photos;
        filtered = _settings.PhotoFilter switch
        {
            PhotoFilterMode.Today => filtered.Where(photo => GetPhotoDate(photo).Date == DateTime.Today),
            PhotoFilterMode.Latest10 => filtered.Take(10),
            PhotoFilterMode.Latest50 => filtered.Take(50),
            PhotoFilterMode.Flagged => filtered.Where(photo => photo.IsFlagged),
            PhotoFilterMode.DateRange => filtered.Where(IsInConfiguredDateRange),
            _ => filtered
        };

        Photos.ReplaceAll(filtered);
    }

    private bool IsInConfiguredDateRange(PhotoItem photo)
    {
        var date = GetPhotoDate(photo).Date;
        var from = _settings.FilterDateFrom?.Date ?? DateTime.MinValue.Date;
        var to = _settings.FilterDateTo?.Date ?? DateTime.MaxValue.Date;
        return date >= from && date <= to;
    }

    private static DateTime GetPhotoDate(PhotoItem photo) =>
        photo.Exif.DateTaken ?? photo.DiscoveredAtUtc.ToLocalTime();

    private void OnFolderAvailabilityChanged(bool available, string? message)
    {
        IsFolderAvailable = available;
        FolderAvailabilityMessage = message ?? string.Empty;
    }
}
