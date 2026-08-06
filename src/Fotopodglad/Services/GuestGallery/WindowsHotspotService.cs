using System.Net.NetworkInformation;
using Fotopodglad.Configuration;
using Windows.Networking.Connectivity;
using Windows.Networking.NetworkOperators;

namespace Fotopodglad.Services.GuestGallery;

/// <summary>
/// Tworzy odizolowany hotspot WiFi przez Windows Mobile Hotspot (NetworkOperatorTetheringManager) —
/// dokładnie ta sama funkcja, co ręczne włączenie "Hotspotu mobilnego" w ustawieniach Windows.
/// Działa równolegle z istniejącym połączeniem WiFi komputera (np. z siecią karty pamięci aparatu)
/// TYLKO jeśli karta sieciowa/sterownik wspiera jednoczesny tryb access-point + klient (Wi-Fi Direct
/// Virtual Adapter) — to zależy od sprzętu i wykrywamy to dopiero przy próbie startu.
/// SSID i hasło: albo te ustawione ręcznie przez użytkownika w oknie ustawień, albo (domyślnie)
/// generowane losowo przy każdym starcie — w obu przypadkach nigdy nie są pokazywane w UI,
/// dostępne wyłącznie zakodowane w kodzie QR.
/// </summary>
public sealed class WindowsHotspotService : IHotspotService
{
    private readonly AppSettings _settings;
    private NetworkOperatorTetheringManager? _tetheringManager;

    public string? Ssid { get; private set; }
    public string? Passphrase { get; private set; }
    public string? LocalIpAddress { get; private set; }

    public WindowsHotspotService(AppSettings settings)
    {
        _settings = settings;
    }

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var profile = NetworkInformation.GetInternetConnectionProfile();
            if (profile is null)
            {
                return false;
            }

            _tetheringManager = NetworkOperatorTetheringManager.CreateFromConnectionProfile(profile);

            Ssid = string.IsNullOrWhiteSpace(_settings.WifiSsid) ? GenerateSsid() : _settings.WifiSsid;
            Passphrase = string.IsNullOrWhiteSpace(_settings.WifiPassphrase) ? GeneratePassphrase() : _settings.WifiPassphrase;

            var config = new NetworkOperatorTetheringAccessPointConfiguration
            {
                Ssid = Ssid,
                Passphrase = Passphrase
            };
            await _tetheringManager.ConfigureAccessPointAsync(config);

            var result = await _tetheringManager.StartTetheringAsync();
            if (result.Status != TetheringOperationStatus.Success)
            {
                _tetheringManager = null;
                return false;
            }

            // StartTetheringAsync może zakończyć się zanim wirtualny adapter dostanie adres IP.
            // Szczególnie w szybkim starcie bez otwierania ustawień potrzebne jest krótkie oczekiwanie.
            LocalIpAddress = await WaitForHotspotLocalIpAsync(cancellationToken);
            if (LocalIpAddress is not null)
            {
                return true;
            }

            await StopAsync();
            return false;
        }
        catch (Exception)
        {
            // Dowolny błąd (brak wsparcia sprzętowego, brak uprawnień, starszy Windows bez tej funkcji
            // dla aplikacji niepakietowanej MSIX) traktujemy jako "funkcja niedostępna na tym sprzęcie".
            await StopAsync();
            return false;
        }
    }

    public async Task StopAsync()
    {
        if (_tetheringManager is null)
        {
            return;
        }

        try
        {
            await _tetheringManager.StopTetheringAsync();
        }
        catch (Exception)
        {
        }
        finally
        {
            _tetheringManager = null;
            LocalIpAddress = null;
        }
    }

    private static string GenerateSsid()
    {
        var suffix = Random.Shared.Next(1000, 9999);
        return $"FotoSesja-{suffix}";
    }

    private static string GeneratePassphrase()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // bez znaków mylących się wizualnie
        Span<char> chars = stackalloc char[10];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[Random.Shared.Next(alphabet.Length)];
        }

        return new string(chars);
    }

    /// <summary>
    /// Windows Mobile Hotspot (oparte o ICS) zwykle przypisuje hostowi stały adres 192.168.137.1
    /// na wirtualnym adapterze hotspotu. Sprawdzamy realne interfejsy sieciowe, żeby to potwierdzić
    /// zamiast ślepo zakładać — jeśli się nie zgadza, traktujemy start jako nieudany.
    /// </summary>
    private static string? DetectHotspotLocalIp()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            foreach (var addr in ni.GetIPProperties().UnicastAddresses)
            {
                var ip = addr.Address.ToString();
                if (ip.StartsWith("192.168.137.", StringComparison.Ordinal))
                {
                    return ip;
                }
            }
        }

        return null;
    }

    private static async Task<string?> WaitForHotspotLocalIpAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (DetectHotspotLocalIp() is { } ipAddress)
            {
                return ipAddress;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        return null;
    }
}
