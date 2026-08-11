using System.Net;
using System.Net.Sockets;
using System.Text;
using Fotopodglad.Configuration;
using Fotopodglad.Models;
using Fotopodglad.Services;

namespace Fotopodglad.Services.GuestGallery;

/// <summary>
/// Minimalny serwer HTTP udostępniający pełne zdjęcie pod /photo/{sequenceId}.
/// TcpListener nie korzysta z HTTP.sys, więc aplikacja nie wymaga uruchamiania jako administrator
/// ani wcześniejszego tworzenia rezerwacji URL ACL na komputerze fotografa.
/// </summary>
public sealed class GuestGalleryHttpServer : IDisposable
{
    private readonly IPhotoLibraryService _library;
    private readonly AppSettings _settings;
    private readonly GuestPhotoExporter _exporter = new();
    private readonly int _configuredPort;
    private readonly SemaphoreSlim _downloadSlots;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public event Action? PhotoDownloaded;
    public event Action? StateChanged;

    public GuestGalleryHttpServer(IPhotoLibraryService library, AppSettings? settings = null, int port = 8080)
    {
        if (port is < 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        _library = library;
        _settings = settings ?? new AppSettings();
        _configuredPort = port;
        Port = port;
        _downloadSlots = new SemaphoreSlim(Math.Clamp(_settings.MaxConcurrentDownloads, 1, 20));
    }

    public int Port { get; private set; }
    public int ActiveDownloads { get; private set; }

    public void Start(string localIpAddress)
    {
        Stop();

        if (!IPAddress.TryParse(localIpAddress, out var address))
        {
            throw new ArgumentException("Nieprawidłowy adres IP hotspotu.", nameof(localIpAddress));
        }

        _cts = new CancellationTokenSource();
        // Adapter Mobile Hotspot potrafi zmienić swój stan pomiędzy wykryciem adresu a Start().
        // Nasłuch na Any eliminuje błąd WSAEADDRNOTAVAIL („adres nieprawidłowy w tym kontekście”),
        // natomiast QR nadal reklamuje wyłącznie przekazany adres lokalny sieci hotspotu.
        var listenAddress = address.AddressFamily == AddressFamily.InterNetworkV6
            ? IPAddress.IPv6Any
            : IPAddress.Any;
        _listener = new TcpListener(listenAddress, _configuredPort);
        try
        {
            _listener.Start();
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse && _configuredPort != 0)
        {
            // Poprzednia instancja aplikacji, antywirus albo inna usługa może jeszcze trzymać 8080.
            // QR zawiera Port odczytany po starcie, więc bezpiecznie przechodzimy na wolny port systemowy.
            _listener.Stop();
            _listener = new TcpListener(listenAddress, 0);
            _listener.Start();
        }
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _ = AcceptLoopAsync(_listener, _cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _listener?.Stop();
        _listener = null;
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken);
                _ = HandleClientAsync(client, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (SocketException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken serverCancellationToken)
    {
        // Ustawienia są czytane raz na żądanie, więc zmiana rozmiaru lub formatu w Ctrl+P działa od razu,
        // bez restartu hotspotu, a w trakcie jednego pobierania nie zmienia się w połowie pliku.
        var downloadOptions = _settings.CreateGuestDownloadOptions();

        using (client)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(serverCancellationToken))
        {
            // Przekodowanie 60-megapikselowego RAW-a trwa dłużej niż przesłanie gotowego pliku,
            // więc limit czasu żądania rośnie tylko wtedy, gdy naprawdę coś przetwarzamy.
            timeout.CancelAfter(downloadOptions.RequiresProcessing ? TimeSpan.FromSeconds(120) : TimeSpan.FromSeconds(15));
            var cancellationToken = timeout.Token;

            try
            {
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
                var requestLine = await reader.ReadLineAsync(cancellationToken);

                string? headerLine;
                do
                {
                    headerLine = await reader.ReadLineAsync(cancellationToken);
                }
                while (!string.IsNullOrEmpty(headerLine));

                if (!TryGetSequenceId(requestLine, out var sequenceId))
                {
                    await WriteErrorAsync(stream, HttpStatusCode.NotFound, cancellationToken);
                    return;
                }

                var photo = _library.Photos.FirstOrDefault(p => p.SequenceId == sequenceId);
                if (photo is null || !File.Exists(photo.FilePath))
                {
                    await WriteErrorAsync(stream, HttpStatusCode.NotFound, cancellationToken);
                    return;
                }

                if (!await _downloadSlots.WaitAsync(0, cancellationToken))
                {
                    await WriteErrorAsync(stream, HttpStatusCode.TooManyRequests, cancellationToken);
                    return;
                }

                Interlocked.Increment(ref activeDownloadsBackingField);
                ActiveDownloads = Volatile.Read(ref activeDownloadsBackingField);
                StateChanged?.Invoke();

                try
                {
                    await SendPhotoAsync(stream, photo, downloadOptions, cancellationToken);
                    PhotoDownloaded?.Invoke();
                }
                finally
                {
                    _downloadSlots.Release();
                    Interlocked.Decrement(ref activeDownloadsBackingField);
                    ActiveDownloads = Volatile.Read(ref activeDownloadsBackingField);
                    StateChanged?.Invoke();
                }
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
            {
                // Telefon przerwał połączenie albo serwer jest właśnie zatrzymywany.
            }
        }
    }

    private int activeDownloadsBackingField;

    private async Task SendPhotoAsync(
        NetworkStream stream,
        PhotoItem photo,
        GuestDownloadOptions options,
        CancellationToken cancellationToken)
    {
        // Dekodowanie i kompresja idą na wątek tła — pętla akceptująca połączenia nie może na nie czekać.
        var payload = options.RequiresProcessing
            ? await Task.Run(
                () => _exporter.TryPrepare(photo.FilePath, photo.FileName, options, cancellationToken),
                cancellationToken)
            : null;

        if (payload is null)
        {
            await SendOriginalFileAsync(stream, photo, cancellationToken);
            return;
        }

        await WriteHeadersAsync(stream, payload.ContentType, payload.FileName, payload.Bytes.Length, cancellationToken);
        await stream.WriteAsync(payload.Bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task SendOriginalFileAsync(NetworkStream stream, PhotoItem photo, CancellationToken cancellationToken)
    {
        await using var photoStream = new FileStream(
            photo.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 81920, useAsync: true);

        await WriteHeadersAsync(stream, GetContentType(photo.FilePath), photo.FileName, photoStream.Length, cancellationToken);
        await photoStream.CopyToAsync(stream, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task WriteHeadersAsync(
        NetworkStream stream,
        string contentType,
        string fileName,
        long contentLength,
        CancellationToken cancellationToken)
    {
        var safeFileName = fileName.Replace("\"", "'").Replace("\r", "").Replace("\n", "");
        var headers =
            "HTTP/1.1 200 OK\r\n" +
            $"Content-Type: {contentType}\r\n" +
            $"Content-Disposition: attachment; filename=\"{safeFileName}\"\r\n" +
            $"Content-Length: {contentLength}\r\n" +
            "Connection: close\r\n\r\n";

        await stream.WriteAsync(Encoding.ASCII.GetBytes(headers), cancellationToken);
    }

    private static string GetContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".tif" or ".tiff" => "image/tiff",
        ".heic" or ".heif" => "image/heic",
        ".dng" => "image/x-adobe-dng",
        _ => "application/octet-stream"
    };

    private static bool TryGetSequenceId(string? requestLine, out long sequenceId)
    {
        sequenceId = 0;
        if (string.IsNullOrWhiteSpace(requestLine))
        {
            return false;
        }

        var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !string.Equals(parts[0], "GET", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        const string prefix = "/photo/";
        var path = parts[1].Split('?', 2)[0];
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               long.TryParse(path[prefix.Length..].TrimEnd('/'), out sequenceId);
    }

    private static async Task WriteErrorAsync(NetworkStream stream, HttpStatusCode statusCode, CancellationToken cancellationToken)
    {
        var reason = statusCode switch
        {
            HttpStatusCode.NotFound => "Not Found",
            HttpStatusCode.TooManyRequests => "Too Many Requests",
            _ => "Bad Request"
        };
        var body = Encoding.UTF8.GetBytes(reason);
        var headers =
            $"HTTP/1.1 {(int)statusCode} {reason}\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n" +
            $"Content-Length: {body.Length}\r\n" +
            "Connection: close\r\n\r\n";

        await stream.WriteAsync(Encoding.ASCII.GetBytes(headers), cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
    }

    public void Dispose() => Stop();
}
