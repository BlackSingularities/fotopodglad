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
/// w Oknie A. Domyślnie pokazuje wyłącznie zdjęcie wskazane przez użytkownika. Opcjonalny tryb
/// automatyczny śledzi najnowsze zdjęcie, z czasowym zatrzymaniem po kliknięciu miniatury.
/// </summary>
public sealed partial class FullscreenPhotoViewModel : ViewModelBase
{
    private readonly IPhotoLibraryService _library;
    private readonly AppSettings _settings;
    private readonly IExifService _exifService;
    private readonly DispatcherTimer _manualHoldTimer;
    private bool _automaticallyShowLatestPhoto;
    private int _loadToken;
    private CancellationTokenSource? _imageLoadCts;
    private CancellationTokenSource? _analysisCts;
    private CancellationTokenSource? _metadataLoadCts;

    public event Action? ZoomResetRequested;

    [ObservableProperty]
    private PhotoItem? currentPhoto;

    [ObservableProperty]
    private BitmapImage? currentImageSource;

    [ObservableProperty]
    private PreviewMode mode = PreviewMode.Manual;

    [ObservableProperty]
    private bool showPhotoParameters;

    [ObservableProperty]
    private double exifTextSize = 15;

    [ObservableProperty]
    private BitmapSource? histogramImage;

    [ObservableProperty]
    private BitmapSource? clippingOverlaySource;

    public ObservableCollection<ExifFieldViewModel> ExifFields { get; } = new();

    public FullscreenPhotoViewModel(IPhotoLibraryService library, AppSettings settings, IExifService? exifService = null)
    {
        _library = library;
        _settings = settings;
        _automaticallyShowLatestPhoto = settings.AutomaticallyShowLatestPhoto;
        _exifService = exifService ?? new ExifService();
        showPhotoParameters = settings.ShowPhotoParameters;
        exifTextSize = settings.ExifTextSize * AppearanceService.ScaleFactor(settings);
        var manualHoldDuration = TimeSpan.FromSeconds(Math.Clamp(
            settings.ManualHoldSeconds,
            AppSettings.MinManualHoldSeconds,
            AppSettings.MaxManualHoldSeconds));
        _manualHoldTimer = new DispatcherTimer { Interval = manualHoldDuration };
        _manualHoldTimer.Tick += (_, _) =>
        {
            _manualHoldTimer.Stop();
            if (_automaticallyShowLatestPhoto)
            {
                ShowLatestAutomatically();
            }
        };

        _library.NewestChanged += OnNewestChanged;
        _settings.Changed += OnSettingsChanged;

        if (_automaticallyShowLatestPhoto)
        {
            Mode = PreviewMode.Auto;
            if (_library.Latest is { } latest)
            {
                SetPhoto(latest);
            }
        }
    }

    public void ShowLatestAutomatically()
    {
        _manualHoldTimer.Stop();
        Mode = _automaticallyShowLatestPhoto ? PreviewMode.Auto : PreviewMode.Manual;
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
        if (_automaticallyShowLatestPhoto)
        {
            _manualHoldTimer.Start();
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        ShowPhotoParameters = _settings.ShowPhotoParameters;
        ExifTextSize = Math.Clamp(_settings.ExifTextSize, 10, 32) * AppearanceService.ScaleFactor(_settings);
        _manualHoldTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(
            _settings.ManualHoldSeconds,
            AppSettings.MinManualHoldSeconds,
            AppSettings.MaxManualHoldSeconds));

        var automaticallyShowLatestPhoto = _settings.AutomaticallyShowLatestPhoto;
        if (_automaticallyShowLatestPhoto != automaticallyShowLatestPhoto)
        {
            _automaticallyShowLatestPhoto = automaticallyShowLatestPhoto;
            _manualHoldTimer.Stop();

            if (!_automaticallyShowLatestPhoto)
            {
                Mode = PreviewMode.Manual;
            }
            else if (CurrentPhoto is null)
            {
                ShowLatestAutomatically();
            }
            else
            {
                Mode = PreviewMode.Manual;
                _manualHoldTimer.Start();
            }
        }

        if (CurrentImageSource is { } source)
        {
            _ = AnalyzeImageAsync(source);
        }
    }

    public void ShowPrevious()
    {
        var index = CurrentPhoto is null ? -1 : _library.Photos.IndexOf(CurrentPhoto);
        if (index >= 0 && index + 1 < _library.Photos.Count)
        {
            ShowManually(_library.Photos[index + 1]);
        }
    }

    public void ShowNext()
    {
        var index = CurrentPhoto is null ? -1 : _library.Photos.IndexOf(CurrentPhoto);
        if (index > 0)
        {
            ShowManually(_library.Photos[index - 1]);
        }
    }

    public void ResetZoom() => ZoomResetRequested?.Invoke();

    private void OnNewestChanged(PhotoItem newest)
    {
        if (Mode == PreviewMode.Auto)
        {
            SetPhoto(newest);
        }
        // W trybie ręcznym napływające zdjęcia nie zmieniają podglądu. Jeżeli automatyczne
        // śledzenie jest włączone, powrót nastąpi dopiero po wygaśnięciu timera.
    }

    private void SetPhoto(PhotoItem photo)
    {
        if (ReferenceEquals(CurrentPhoto, photo))
        {
            return;
        }

        CurrentPhoto = photo;
        BuildExifFields(photo);
        _ = LoadMetadataAsync(photo);
        _ = LoadImageAsync(photo);
    }

    private async Task LoadMetadataAsync(PhotoItem photo)
    {
        if (photo.MetadataLoaded)
        {
            return;
        }

        _metadataLoadCts?.Cancel();
        _metadataLoadCts?.Dispose();
        _metadataLoadCts = new CancellationTokenSource();
        var cancellationToken = _metadataLoadCts.Token;
        try
        {
            var metadata = await _exifService.ExtractAsync(photo.FilePath, cancellationToken);
            if (!cancellationToken.IsCancellationRequested && ReferenceEquals(CurrentPhoto, photo))
            {
                photo.Exif = metadata;
                photo.MetadataLoaded = true;
                BuildExifFields(photo);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task LoadImageAsync(PhotoItem photo)
    {
        _imageLoadCts?.Cancel();
        _imageLoadCts?.Dispose();
        _imageLoadCts = new CancellationTokenSource();
        var cancellationToken = _imageLoadCts.Token;
        var token = ++_loadToken;
        BitmapImage? bitmap;
        try
        {
            bitmap = await Task.Run(
                () => BitmapHelper.LoadFrozen(photo.FilePath, decodePixelWidth: 3840, cancellationToken),
                cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token != _loadToken || cancellationToken.IsCancellationRequested)
        {
            return; // W międzyczasie zażądano innego zdjęcia — porzucamy przestarzały wynik.
        }

        CurrentImageSource = bitmap;
        if (bitmap is not null)
        {
            _ = AnalyzeImageAsync(bitmap);
        }
    }

    private async Task AnalyzeImageAsync(BitmapSource source)
    {
        _analysisCts?.Cancel();
        _analysisCts?.Dispose();
        _analysisCts = new CancellationTokenSource();
        var cancellationToken = _analysisCts.Token;

        try
        {
            var result = await Task.Run(() =>
            {
                var histogram = ImageAnalysisService.CreateHistogram(source, _settings.Histogram, cancellationToken);
                var clipping = _settings.ShowClippingWarnings
                    ? ImageAnalysisService.CreateClippingOverlay(source, cancellationToken)
                    : null;
                return (histogram, clipping);
            }, cancellationToken);

            if (!cancellationToken.IsCancellationRequested)
            {
                HistogramImage = result.histogram;
                ClippingOverlaySource = result.clipping;
            }
        }
        catch (OperationCanceledException)
        {
        }
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

        if (exif.ExposureProgram is { } exposureProgram)
        {
            ExifFields.Add(new ExifFieldViewModel("Icon.ExposureProgram", exposureProgram));
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
