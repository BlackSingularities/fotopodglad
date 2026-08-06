using System.Runtime.InteropServices;

namespace Fotopodglad.Helpers;

/// <summary>
/// SetWindowPos w pikselach fizycznych — używane do umieszczania okien na konkretnym monitorze.
/// WPF Window.Left/Top/Width/Height operują w DIP (jednostkach niezależnych od DPI), przeliczanych
/// względem DPI monitora, na którym window "aktualnie myśli że jest" — przy niestandardowym
/// skalowaniu (125%, 150%...) prowadziło to do złego rozmiaru/pozycji okna i w efekcie do sytuacji,
/// w której jedno okno nachodziło na drugie zamiast każde zajmować własny ekran. SetWindowPos na
/// surowym uchwycie HWND omija ten problem całkowicie — działa w tych samych jednostkach co
/// Screen.Bounds (System.Windows.Forms), więc nie trzeba w ogóle liczyć DPI.
/// </summary>
internal static class Win32Interop
{
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    public static void SetWindowBounds(IntPtr hwnd, int left, int top, int width, int height)
    {
        SetWindowPos(hwnd, IntPtr.Zero, left, top, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
