using System.Runtime.InteropServices;
using System.Windows.Forms;
using Fotopodglad.Models;

namespace Fotopodglad.Services;

/// <summary>
/// Enumeruje monitory podłączone do komputera i przelicza ich geometrię z pikseli fizycznych
/// na jednostki niezależne od DPI (DIP, 1/96"), których oczekuje WPF przy ustawianiu Left/Top/Width/Height.
/// Aplikacja jest oznaczona jako Per-Monitor-V2 DPI aware (app.manifest), więc Screen.Bounds zwraca
/// rzeczywiste piksele fizyczne danego monitora — trzeba je podzielić przez jego własny współczynnik DPI.
/// </summary>
public sealed class ScreenService : IScreenService
{
    public IReadOnlyList<ScreenInfo> GetScreens()
    {
        var screens = Screen.AllScreens;
        var result = new List<ScreenInfo>(screens.Length);

        foreach (var screen in screens)
        {
            var dpiScale = GetDpiScaleForScreen(screen);

            result.Add(new ScreenInfo
            {
                DeviceName = screen.DeviceName,
                Left = screen.Bounds.Left / dpiScale,
                Top = screen.Bounds.Top / dpiScale,
                Width = screen.Bounds.Width / dpiScale,
                Height = screen.Bounds.Height / dpiScale,
                IsPrimary = screen.Primary
            });
        }

        return result;
    }

    private static double GetDpiScaleForScreen(Screen screen)
    {
        try
        {
            var centerPoint = new POINT
            {
                X = screen.Bounds.Left + screen.Bounds.Width / 2,
                Y = screen.Bounds.Top + screen.Bounds.Height / 2
            };

            var monitorHandle = MonitorFromPoint(centerPoint, MONITOR_DEFAULTTONEAREST);
            var hr = GetDpiForMonitor(monitorHandle, MonitorDpiType.MDT_EFFECTIVE_DPI, out var dpiX, out _);
            if (hr == 0 && dpiX > 0)
            {
                return dpiX / 96.0;
            }
        }
        catch (DllNotFoundException)
        {
            // Windows starsze niż 8.1 — brak shcore.dll, wracamy do skali 1.0.
        }
        catch (EntryPointNotFoundException)
        {
        }

        return 1.0;
    }

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private enum MonitorDpiType
    {
        MDT_EFFECTIVE_DPI = 0
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);
}
