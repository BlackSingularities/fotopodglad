using System.Windows.Forms;
using Fotopodglad.Models;

namespace Fotopodglad.Services;

/// <summary>
/// Enumeruje monitory podłączone do komputera. Zwraca surowe piksele fizyczne (Screen.Bounds) —
/// żadnego przeliczania przez DPI, bo okna są pozycjonowane przez Win32 SetWindowPos w tych samych
/// jednostkach (zob. App.ConfigureFullscreenWindow), co eliminuje niejednoznaczności DIP-na-ekran
/// przy niestandardowym skalowaniu (125%, 150% itd.), które powodowały złe rozmieszczenie okien.
/// </summary>
public sealed class ScreenService : IScreenService
{
    public IReadOnlyList<ScreenInfo> GetScreens()
    {
        return Screen.AllScreens
            .Select(screen => new ScreenInfo
            {
                DeviceName = screen.DeviceName,
                Left = screen.Bounds.Left,
                Top = screen.Bounds.Top,
                Width = screen.Bounds.Width,
                Height = screen.Bounds.Height,
                IsPrimary = screen.Primary
            })
            .ToList();
    }
}
