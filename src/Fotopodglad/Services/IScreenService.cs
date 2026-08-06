using Fotopodglad.Models;

namespace Fotopodglad.Services;

public interface IScreenService
{
    IReadOnlyList<ScreenInfo> GetScreens();
}
