using System.Net;
using System.Net.Sockets;
using System.Text;
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
    private readonly int _configuredPort;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public event Action? PhotoDownloaded;

    public GuestGalleryHttpServer(IPhotoLibraryService library, int port = 8080)
    {
        if (port is < 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        _library = library;
        _configuredPort = port;
        Port = port;
    }

    public int Port { get; private set; }

    public void Start(string localIpAddress)
    {
        Stop();

        if (!IPAddress.TryParse(localIpAddress, out var address))
        {
            throw new ArgumentException("Nieprawidłowy adres IP hotspotu.", nameof(localIpAddress));
        }

        _cts = new CancellationTokenSource();
        _listener = new TcpListener(address, _configuredPort);
        _listener.Start();
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
        using (client)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(serverCancellationToken))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
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

                var safeFileName = photo.FileName.Replace("\"", "'").Replace("\r", "").Replace("\n", "");
                await using var photoStream = new FileStream(
                    photo.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
                    bufferSize: 81920, useAsync: true);

                var headers =
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: image/jpeg\r\n" +
                    $"Content-Disposition: attachment; filename=\"{safeFileName}\"\r\n" +
                    $"Content-Length: {photoStream.Length}\r\n" +
                    "Connection: close\r\n\r\n";

                await stream.WriteAsync(Encoding.ASCII.GetBytes(headers), cancellationToken);
                await photoStream.CopyToAsync(stream, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                PhotoDownloaded?.Invoke();
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
            {
                // Telefon przerwał połączenie albo serwer jest właśnie zatrzymywany.
            }
        }
    }

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
        var reason = statusCode == HttpStatusCode.NotFound ? "Not Found" : "Bad Request";
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
