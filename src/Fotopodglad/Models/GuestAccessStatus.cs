namespace Fotopodglad.Models;

public enum GuestAccessStatus
{
    /// <summary>Nie sprawdzono jeszcze wsparcia sprzętowego.</summary>
    Unknown,

    /// <summary>Sprzęt/sterownik nie wspiera jednoczesnego trybu access-point + połączenie klienckie — funkcja gości wyłączona.</summary>
    Unsupported,

    /// <summary>Hotspot i serwer działają, goście mogą pobierać zdjęcia.</summary>
    Active,

    /// <summary>Hotspot wyłączony po 5 minutach bezczynności — zostanie wznowiony przy nowym zdjęciu.</summary>
    IdleStopped
}
