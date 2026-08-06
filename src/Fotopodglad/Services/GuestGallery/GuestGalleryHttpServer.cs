using System.Net;
using Fotopodglad.Services;

namespace Fotopodglad.Services.GuestGallery;

/// <summary>
/// Minimalny lokalny serwer HTTP (bez ASP.NET Core — HttpListener wystarcza do jednego prostego
/// endpointu). Serwuje pełnorozdzielczy JPG jako załącznik pod /photo/{sequenceId}, dzięki czemu
/// otwarcie linku w przeglądarce telefonu od razu uruchamia pobieranie, bez pośredniej strony HTML.
/// Bindowanie do konkretnego adresu IP hotspotu (nie do symbolu wieloznacznego "+") nie wymaga
/// uprawnień administratora ani rezerwacji URL ACL.
/// </summary>
public sealed class GuestGalleryHttpServer : IDisposable
{
    private readonly IPhotoLibraryService _library;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;

    public event Action? PhotoDownloaded;

    public GuestGalleryHttpServer(IPhotoLibraryService library)
    {
        _library = library;
    }

    public int Port { get; } = 8080;

    public void Start(string localIpAddress)
    {
        Stop();

        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://{localIpAddress}:{Port}/photo/");
        _listener.Start();

        _ = AcceptLoopAsync(_listener, _cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_listener is { IsListening: true })
        {
            _listener.Stop();
        }
        _listener?.Close();
        _listener = null;
    }

    private async Task AcceptLoopAsync(HttpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                continue;
            }

            _ = HandleRequestAsync(context);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            var segment = context.Request.Url?.Segments.LastOrDefault()?.TrimEnd('/');
            var photo = long.TryParse(segment, out var sequenceId)
                ? _library.Photos.FirstOrDefault(p => p.SequenceId == sequenceId)
                : null;

            if (photo is null || !File.Exists(photo.FilePath))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                context.Response.Close();
                return;
            }

            var bytes = await File.ReadAllBytesAsync(photo.FilePath);
            context.Response.ContentType = "image/jpeg";
            context.Response.AddHeader("Content-Disposition", $"attachment; filename=\"{photo.FileName}\"");
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.OutputStream.Close();

            PhotoDownloaded?.Invoke();
        }
        catch (Exception)
        {
            try
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.Close();
            }
            catch (Exception)
            {
            }
        }
    }

    public void Dispose() => Stop();
}
