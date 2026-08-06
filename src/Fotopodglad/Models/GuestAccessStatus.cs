namespace Fotopodglad.Models;

public enum GuestAccessStatus
{
    /// <summary>Nie sprawdzono jeszcze wsparcia sprzętowego.</summary>
    Unknown,

    /// <summary>Trwa uruchamianie albo kontrolowana naprawa hotspotu.</summary>
    Starting,

    /// <summary>Trwa reset sesji gościa i generowanie nowego hasła.</summary>
    Resetting,

    /// <summary>Sprzęt/sterownik nie wspiera jednoczesnego trybu access-point + połączenie klienckie — funkcja gości wyłączona.</summary>
    Unsupported,

    /// <summary>Hotspot i serwer działają, goście mogą pobierać zdjęcia.</summary>
    Active,

    /// <summary>System lub sterownik zgłosił błąd możliwy do ponowienia.</summary>
    DriverError,

    /// <summary>Hotspot został jawnie zatrzymany podczas zamykania lub restartu aplikacji.</summary>
    IdleStopped
}
