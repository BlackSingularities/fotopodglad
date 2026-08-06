using Fotopodglad.Configuration;
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
    public void ManualPreviewMaximum_IsFifteenMinutes()
    {
        Assert.Equal(900, AppSettings.MaxManualHoldSeconds);
    }
}
