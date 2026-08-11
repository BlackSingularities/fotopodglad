using Fotopodglad.Configuration;
using Fotopodglad.Models;
using Xunit;

namespace Fotopodglad.Tests.Configuration;

public sealed class AppSettingsTests
{
    [Fact]
    public void Defaults_EnableGuestHotspot()
    {
        var settings = new AppSettings();

        Assert.True(settings.GuestAccessEnabled);
    }

    [Fact]
    public void Defaults_ShowPhotoParameters()
    {
        var settings = new AppSettings();

        Assert.True(settings.ShowPhotoParameters);
    }

    [Fact]
    public void Defaults_ShowGuestInstructions()
    {
        var settings = new AppSettings();

        Assert.True(settings.ShowGuestInstructions);
    }

    [Fact]
    public void Defaults_KeepSelectedPhotoInsteadOfFollowingLatest()
    {
        var settings = new AppSettings();

        Assert.False(settings.AutomaticallyShowLatestPhoto);
    }

    [Fact]
    public void ManualPreviewMaximum_IsFifteenMinutes()
    {
        Assert.Equal(900, AppSettings.MaxManualHoldSeconds);
    }

    [Fact]
    public void VersionTwoDefaults_AreSafeForMemoryAndNetwork()
    {
        var settings = new AppSettings();

        Assert.Equal(3, settings.MaxConcurrentDownloads);
        Assert.Equal(256, settings.ThumbnailCacheMegabytes);
        Assert.Equal(HistogramMode.Off, settings.Histogram);
        Assert.True(settings.CheckForUpdates);
    }

    [Fact]
    public void Clone_HasIndependentFlaggedListAndPlacements()
    {
        var original = new AppSettings { FlaggedPhotoPaths = ["one.jpg"] };
        original.MainWindowPlacement.Left = 10;
        var clone = original.Clone();

        clone.FlaggedPhotoPaths.Add("two.jpg");
        clone.MainWindowPlacement.Left = 99;

        Assert.Single(original.FlaggedPhotoPaths);
        Assert.Equal(10, original.MainWindowPlacement.Left);
    }

    [Fact]
    public void Defaults_SendOriginalFileToGuests()
    {
        var options = new AppSettings().CreateGuestDownloadOptions();

        Assert.Equal(GuestDownloadOptions.OriginalSize, options.LongestEdgePixels);
        Assert.False(options.ConvertToJpeg);
        Assert.False(options.RequiresProcessing);
    }

    [Fact]
    public void GuestDownloadOptions_RequireProcessingWheneverSizeIsLimited()
    {
        var settings = new AppSettings { GuestDownloadLongestEdge = 2048, GuestDownloadJpegQuality = 150 };

        var options = settings.CreateGuestDownloadOptions();

        Assert.True(options.RequiresProcessing);
        Assert.Equal(GuestDownloadOptions.MaxJpegQuality, options.ClampedJpegQuality);
    }

    [Fact]
    public void CopyFrom_CarriesGuestDownloadSettings()
    {
        var target = new AppSettings();
        var source = new AppSettings
        {
            GuestDownloadLongestEdge = 1600,
            GuestDownloadConvertToJpeg = true,
            GuestDownloadJpegQuality = 75
        };

        target.CopyFrom(source);

        Assert.Equal(1600, target.GuestDownloadLongestEdge);
        Assert.True(target.GuestDownloadConvertToJpeg);
        Assert.Equal(75, target.GuestDownloadJpegQuality);
    }

    [Fact]
    public void GuestDownloadChanges_DoNotRequireApplicationRestart()
    {
        var before = new AppSettings();
        var after = before.Clone();
        after.GuestDownloadLongestEdge = 2048;
        after.GuestDownloadConvertToJpeg = true;
        after.GuestDownloadJpegQuality = 80;

        // Serwer czyta ustawienia przy każdym żądaniu, więc hotspot nie musi się restartować.
        Assert.False(AppSettings.RequiresRestart(before, after));
    }

    [Fact]
    public void VisualChanges_DoNotRequireApplicationRestart()
    {
        var before = new AppSettings();
        var after = before.Clone();
        after.GridColumnCount = 9;
        after.QrCodeSize = 240;
        after.Theme = ThemeMode.Light;

        Assert.False(AppSettings.RequiresRestart(before, after));
    }
}
