namespace Fotopodglad.Helpers;

/// <summary>
/// Sprawdza, czy plik zapisywany przez kartę WiFi jest już w pełni zapisany, zanim spróbujemy go odczytać.
/// Karty WiFi różnie się zachowują — niektóre trzymają uchwyt pliku otwarty przez cały zapis, inne nie —
/// więc stosujemy podwójne zabezpieczenie: próba otwarcia na wyłączność ORAZ stabilny rozmiar w kolejnych odczytach.
/// </summary>
public static class FileStabilityChecker
{
    public static async Task<bool> WaitUntilStableAsync(
        string filePath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var pollInterval = TimeSpan.FromMilliseconds(200);
        var deadline = DateTime.UtcNow + timeout;
        long lastSize = -1;
        var stableReadings = 0;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(filePath))
            {
                await Task.Delay(pollInterval, cancellationToken);
                continue;
            }

            try
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                var currentSize = stream.Length;

                if (currentSize > 0 && currentSize == lastSize)
                {
                    stableReadings++;
                    if (stableReadings >= 2)
                    {
                        return true;
                    }
                }
                else
                {
                    stableReadings = 0;
                }

                lastSize = currentSize;
            }
            catch (IOException)
            {
                // Plik wciąż zablokowany przez proces zapisujący — spróbuj ponownie po chwili.
                stableReadings = 0;
            }
            catch (UnauthorizedAccessException)
            {
                stableReadings = 0;
            }

            await Task.Delay(pollInterval, cancellationToken);
        }

        return false;
    }
}
