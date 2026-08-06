using Fotopodglad.Models;

namespace Fotopodglad.Services;

public interface IExifService
{
    Task<ExifData> ExtractAsync(string filePath, CancellationToken cancellationToken = default);
}
