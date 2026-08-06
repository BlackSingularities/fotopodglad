namespace Fotopodglad.Services.GuestGallery;

public interface IHotspotService
{
    string? Ssid { get; }
    string? Passphrase { get; }

    /// <summary>Adres IP komputera w sieci hotspotu (zwykle 192.168.137.1 dla Windows Mobile Hotspot / ICS).</summary>
    string? LocalIpAddress { get; }

    /// <summary>
    /// Próbuje uruchomić izolowany hotspot z losowym SSID/hasłem. Zwraca false, jeśli sprzęt/sterownik
    /// nie wspiera trybu access-point równolegle z istniejącym połączeniem klienckim, albo z innego
    /// powodu się nie uda — w takim wypadku funkcja gości powinna zostać po cichu wyłączona.
    /// </summary>
    Task<bool> StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();
}
