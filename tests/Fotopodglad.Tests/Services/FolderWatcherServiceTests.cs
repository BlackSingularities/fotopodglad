using Fotopodglad.Services;
using Xunit;

namespace Fotopodglad.Tests.Services;

public sealed class FolderWatcherServiceTests : IDisposable
{
    private readonly string _tempFolder;
    private readonly FolderWatcherService _watcher = new();

    public FolderWatcherServiceTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "Fotopodglad.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempFolder);
    }

    [Fact]
    public async Task PhotoReady_NotRaised_WhileFileIsStillBeingWritten()
    {
        var readySignal = new TaskCompletionSource<string>();
        _watcher.PhotoReady += path => readySignal.TrySetResult(path);
        _watcher.Start(_tempFolder);

        var filePath = Path.Combine(_tempFolder, "incoming.jpg");

        // Symulacja karty WiFi zapisującej plik w kawałkach z opóźnieniami.
        await using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var chunk = new byte[1024];
            for (var i = 0; i < 5; i++)
            {
                await stream.WriteAsync(chunk);
                await stream.FlushAsync();

                // Plik nie powinien zostać zgłoszony jako gotowy, dopóki trwa zapis (uchwyt FileShare.None).
                Assert.False(readySignal.Task.IsCompleted);
                await Task.Delay(150);
            }
        }

        var completed = await Task.WhenAny(readySignal.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(readySignal.Task, completed);
        Assert.Equal(filePath, await readySignal.Task);
    }

    [Fact]
    public async Task PhotoReady_Raised_ForFileAlreadyPresentAtStartupViaPolling()
    {
        var filePath = Path.Combine(_tempFolder, "existing.jpg");
        await File.WriteAllBytesAsync(filePath, new byte[2048]);

        var readySignal = new TaskCompletionSource<string>();
        _watcher.PhotoReady += path => readySignal.TrySetResult(path);
        _watcher.Start(_tempFolder);

        var completed = await Task.WhenAny(readySignal.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.Same(readySignal.Task, completed);
    }

    public void Dispose()
    {
        _watcher.Dispose();
        try
        {
            Directory.Delete(_tempFolder, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
