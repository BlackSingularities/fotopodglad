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
