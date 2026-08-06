using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Fotopodglad.Configuration;
using Fotopodglad.Helpers;
using Fotopodglad.Models;
using Fotopodglad.Services;

namespace Fotopodglad.ViewModels;

/// <summary>
/// VM reużywalnej kontrolki pełnoekranowego podglądu (Controls/FullscreenPhotoView), używanej zarówno
/// w Oknie A (zawsze w trybie Auto) jak i w Oknie B jako overlay wywoływany kliknięciem w siatkę
/// (tryb Manual, minimum settings.ManualHoldSeconds, potem automatyczny powrót do najnowszego zdjęcia —
/// pozostając pełnoekranowo).
/// </summary>
public sealed partial class FullscreenPhotoViewModel : ViewModelBase
{
    private readonly IPhotoLibraryService _library;
    private readonly DispatcherTimer _manualHoldTimer;
    private int _loadToken;

    [ObservableProperty]
    private PhotoItem? currentPhoto;

    [ObservableProperty]
    private BitmapImage? currentImageSource;

    [ObservableProperty]
    private PreviewMode mode = PreviewMode.Auto;

    public ObservableCollection<ExifFieldViewModel> ExifFields { get; } = new();

    public FullscreenPhotoViewModel(IPhotoLibraryService library, AppSettings settings)
    {
        _library = library;
        var manualHoldDuration = TimeSpan.FromSeconds(Math.Max(1, settings.ManualHoldSeconds));
        _manualHoldTimer = new DispatcherTimer { Interval = manualHoldDuration };
        _manualHoldTimer.Tick += (_, _) =>
        {
            _manualHoldTimer.Stop();
            ShowLatestAutomatically();
        };

        _library.NewestChanged += OnNewestChanged;

        if (_library.Latest is { } latest)
        {
            SetPhoto(latest);
        }
    }

    public void ShowLatestAutomatically()
    {
        _manualHoldTimer.Stop();
        Mode = PreviewMode.Auto;
        if (_library.Latest is { } latest)
        {
            SetPhoto(latest);
        }
    }

    public void ShowManually(PhotoItem photo)
    {
        Mode = PreviewMode.Manual;
        SetPhoto(photo);
        _manualHoldTimer.Stop();
        _manualHoldTimer.Start();
    }

    private void OnNewestChanged(PhotoItem newest)
    {
        if (Mode == PreviewMode.Auto)
        {
            SetPhoto(newest);
        }
        // W trybie Manual ignorujemy napływające zdjęcia aż do wygaśnięcia _manualHoldTimer —
        // to realizuje wymóg "trzymaj wybrane zdjęcie min. 10s mimo nowszych".
    }

    private void SetPhoto(PhotoItem photo)
    {
        if (ReferenceEquals(CurrentPhoto, photo))
        {
            return;
        }

        CurrentPhoto = photo;
        BuildExifFields(photo);
        _ = LoadImageAsync(photo);
    }

    private async Task LoadImageAsync(PhotoItem photo)
    {
        var token = ++_loadToken;
        var bitmap = await Task.Run(() => BitmapHelper.LoadFrozen(photo.FilePath, decodePixelWidth: 3840));

        if (token != _loadToken)
        {
            return; // W międzyczasie zażądano innego zdjęcia — porzucamy przestarzały wynik.
        }

        CurrentImageSource = bitmap;
    }

    private void BuildExifFields(PhotoItem photo)
    {
        ExifFields.Clear();
        var exif = photo.Exif;

        ExifFields.Add(new ExifFieldViewModel("Icon.FileName", photo.FileName));

        if (exif.DateTaken is { } dateTaken)
        {
            ExifFields.Add(new ExifFieldViewModel("Icon.Clock", dateTaken.ToString("HH:mm:ss", CultureInfo.InvariantCulture)));
        }

        if (exif.ApertureFNumber is { } aperture)
        {
            ExifFields.Add(new ExifFieldViewModel("Icon.Aperture", $"f/{aperture.ToString("0.0#", CultureInfo.InvariantCulture)}"));
        }

        if (exif.ExposureTimeSeconds is { } exposure)
        {
            var text = exposure >= 1
                ? $"{exposure.ToString("0.#", CultureInfo.InvariantCulture)} s"
                : $"1/{Math.Round(1.0 / exposure).ToString(CultureInfo.InvariantCulture)} s";
            ExifFields.Add(new ExifFieldViewModel("Icon.ShutterSpeed", text));
        }

        if (exif.Iso is { } iso)
        {
            ExifFields.Add(new ExifFieldViewModel("Icon.Iso", $"ISO {iso.ToString(CultureInfo.InvariantCulture)}"));
        }

        if (exif.FocalLengthMm is { } focalLength)
        {
            ExifFields.Add(new ExifFieldViewModel("Icon.FocalLength", $"{focalLength.ToString("0.#", CultureInfo.InvariantCulture)} mm"));
        }

        if (exif.ExposureMode is { } exposureMode)
        {
            ExifFields.Add(new ExifFieldViewModel("Icon.ExposureMode", exposureMode));
        }

        if (exif.WhiteBalance is { } whiteBalance)
        {
            ExifFields.Add(new ExifFieldViewModel("Icon.WhiteBalance", whiteBalance));
        }

        if (exif.PixelWidth > 0 && exif.PixelHeight > 0)
        {
            ExifFields.Add(new ExifFieldViewModel("Icon.Resolution", $"{exif.PixelWidth} × {exif.PixelHeight} px"));
        }

        if (exif.FileSizeBytes > 0)
        {
            ExifFields.Add(new ExifFieldViewModel("Icon.FileSize", FormatFileSize(exif.FileSizeBytes)));
        }
    }

    private static string FormatFileSize(long bytes)
    {
        double size = bytes;
        string[] units = ["B", "KB", "MB", "GB"];
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size.ToString(unitIndex == 0 ? "0" : "0.#", CultureInfo.InvariantCulture)} {units[unitIndex]}";
    }
}
