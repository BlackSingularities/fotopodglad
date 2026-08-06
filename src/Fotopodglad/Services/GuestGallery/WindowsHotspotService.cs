using System.Net.NetworkInformation;
using Windows.Networking.Connectivity;
using Windows.Networking.NetworkOperators;

namespace Fotopodglad.Services.GuestGallery;

/// <summary>
/// Tworzy odizolowany hotspot WiFi przez Windows Mobile Hotspot (NetworkOperatorTetheringManager) —
/// dokładnie ta sama funkcja, co ręczne włączenie "Hotspotu mobilnego" w ustawieniach Windows.
/// Działa równolegle z istniejącym połączeniem WiFi komputera (np. z siecią karty pamięci aparatu)
/// TYLKO jeśli karta sieciowa/sterownik wspiera jednoczesny tryb access-point + klient (Wi-Fi Direct
/// Virtual Adapter) — to zależy od sprzętu i wykrywamy to dopiero przy próbie startu.
/// SSID i hasło są generowane losowo przy każdym starcie i nigdy nie są pokazywane w UI —
/// dostępne wyłącznie zakodowane w kodzie QR.
/// </summary>
public sealed class WindowsHotspotService : IHotspotService
{
    private NetworkOperatorTetheringManager? _tetheringManager;

    public string? Ssid { get; private set; }
    public string? Passphrase { get; private set; }
    public string? LocalIpAddress { get; private set; }

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

            Ssid = GenerateSsid();
            Passphrase = GeneratePassphrase();

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

            LocalIpAddress = DetectHotspotLocalIp();
            return LocalIpAddress is not null;
        }
        catch (Exception)
        {
            // Dowolny błąd (brak wsparcia sprzętowego, brak uprawnień, starszy Windows bez tej funkcji
            // dla aplikacji niepakietowanej MSIX) traktujemy jako "funkcja niedostępna na tym sprzęcie".
            _tetheringManager = null;
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
}
