using System.Globalization;
using System.Windows.Media.Imaging;
using Fotopodglad.Models;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Jpeg;
using ImageMagick;

namespace Fotopodglad.Services;

/// <summary>
/// Odczytuje dane EXIF z pliku JPG. Główne źródło to BitmapMetadata (Windows Imaging Component),
/// wbudowane w WPF i pozbawione zewnętrznych zależności. Gdy WIC nie odczyta któregoś pola
/// (nietypowe makernote niektórych aparatów), brakujące wartości są dopełniane przez MetadataExtractor.
/// </summary>
public sealed class ExifService : IExifService
{
    private const string QueryFNumber = "/app1/ifd/exif/{ushort=33437}";
    private const string QueryExposureTime = "/app1/ifd/exif/{ushort=33434}";
    private const string QueryIso = "/app1/ifd/exif/{ushort=34855}";
    private const string QueryFocalLength = "/app1/ifd/exif/{ushort=37386}";
    private const string QueryDateTimeOriginal = "/app1/ifd/exif/{ushort=36867}";
    private const string QueryExposureProgram = "/app1/ifd/exif/{ushort=34850}";
    private const string QueryOrientation = "/app1/ifd/{ushort=274}";

    public Task<ExifData> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
        => Task.Run(() => Extract(filePath), cancellationToken);

    private static ExifData Extract(string filePath)
    {
        int width = 0, height = 0;
        int? orientation = null;
        double? aperture = null, exposureTime = null, focalLength = null;
        int? iso = null;
        DateTime? dateTaken = null;
        string? exposureProgram = null;

        // MetadataExtractor czyta tylko nagłówki/segmenty EXIF. Poprzednio BitmapCacheOption.OnLoad
        // dekodował cały wielomegapikselowy JPEG tylko po to, by poznać kilka pól — przy wielu
        // zdjęciach powodowało to sekundy pracy CPU i bardzo duży chwilowy przydział pamięci.
        TryFillFromMetadataExtractor(
            filePath,
            ref aperture, ref exposureTime, ref focalLength, ref iso, ref dateTaken,
            ref exposureProgram, ref orientation, ref width, ref height);

        if (width <= 0 || height <= 0)
        {
            try
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var frame = BitmapFrame.Create(
                    stream, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
                width = frame.PixelWidth;
                height = frame.PixelHeight;
            }
            catch (Exception ex) when (ex is NotSupportedException or FileFormatException or IOException)
            {
                // RAW lub uszkodzony obraz przejdzie do lekkiego odczytu MagickImageInfo poniżej.
            }
        }

        (width, height) = ApplyOrientationToDimensions(width, height, orientation);

        if (width <= 0 || height <= 0)
        {
            try
            {
                var info = new MagickImageInfo(filePath);
                width = (int)info.Width;
                height = (int)info.Height;
            }
            catch (MagickException)
            {
            }
        }

        long fileSize = 0;
        try
        {
            fileSize = new FileInfo(filePath).Length;
        }
        catch (IOException)
        {
        }

        return new ExifData
        {
            DateTaken = dateTaken,
            ApertureFNumber = aperture,
            ExposureTimeSeconds = exposureTime,
            Iso = iso,
            FocalLengthMm = focalLength,
            ExposureProgram = exposureProgram,
            PixelWidth = width,
            PixelHeight = height,
            FileSizeBytes = fileSize
        };
    }

    private static void TryFillFromMetadataExtractor(
        string filePath,
        ref double? aperture, ref double? exposureTime, ref double? focalLength,
        ref int? iso, ref DateTime? dateTaken, ref string? exposureProgram, ref int? orientation,
        ref int width, ref int height)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(filePath);
            var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            var jpeg = directories.OfType<JpegDirectory>().FirstOrDefault();

            if (subIfd is not null)
            {
                aperture ??= subIfd.TryGetDouble(ExifDirectoryBase.TagFNumber, out var f) ? f : null;
                focalLength ??= subIfd.TryGetDouble(ExifDirectoryBase.TagFocalLength, out var fl) ? fl : null;
                iso ??= subIfd.TryGetInt32(ExifDirectoryBase.TagIsoEquivalent, out var isoVal) ? isoVal : null;
                dateTaken ??= subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dt) ? dt : null;
                exposureProgram ??= subIfd.TryGetInt32(ExifDirectoryBase.TagExposureProgram, out var program)
                    ? GetExposureProgramLabel(program)
                    : null;

                if (exposureTime is null && subIfd.TryGetRational(ExifDirectoryBase.TagExposureTime, out var rational))
                {
                    exposureTime = rational.ToDouble();
                }

                width = subIfd.TryGetInt32(ExifDirectoryBase.TagExifImageWidth, out var exifWidth) ? exifWidth : width;
                height = subIfd.TryGetInt32(ExifDirectoryBase.TagExifImageHeight, out var exifHeight) ? exifHeight : height;
            }

            dateTaken ??= ifd0?.TryGetDateTime(ExifDirectoryBase.TagDateTime, out var ifdDt) == true ? ifdDt : null;
            orientation ??= ifd0?.TryGetInt32(ExifDirectoryBase.TagOrientation, out var orientationValue) == true
                ? orientationValue
                : null;
            width = jpeg?.TryGetInt32(JpegDirectory.TagImageWidth, out var jpegWidth) == true ? jpegWidth : width;
            height = jpeg?.TryGetInt32(JpegDirectory.TagImageHeight, out var jpegHeight) == true ? jpegHeight : height;
        }
        catch (Exception ex) when (ex is IOException or ImageProcessingException)
        {
            // Brak metadanych albo nierozpoznany format — zostają wartości null, UI pokaże tylko dostępne pola.
        }
    }

    private static double? TryGetDouble(BitmapMetadata metadata, string query)
    {
        if (!metadata.ContainsQuery(query))
        {
            return null;
        }

        try
        {
            return DecodeMetadataNumericValue(metadata.GetQuery(query));
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            return null;
        }
    }

    internal static double? DecodeMetadataNumericValue(object? value)
    {
        return value switch
        {
            null => null,
            double d => d,
            float f => f,
            decimal m => (double)m,
            int i => i,
            uint u => u,
            // WIC zwraca EXIF RATIONAL jako ulong: licznik w młodszych 32 bitach,
            // mianownik w starszych. Np. 0x0000000A00000038 oznacza 56/10 = f/5.6.
            ulong ul => DecodeUnsignedRational(ul),
            long l => DecodeSignedRational(l),
            string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => Convert.ToDouble(value, CultureInfo.InvariantCulture)
        };
    }

    private static double? DecodeUnsignedRational(ulong packed)
    {
        var numerator = (uint)(packed & uint.MaxValue);
        var denominator = (uint)(packed >> 32);
        return denominator == 0 ? numerator : (double)numerator / denominator;
    }

    private static double? DecodeSignedRational(long packed)
    {
        var numerator = unchecked((int)(packed & uint.MaxValue));
        var denominator = unchecked((int)((ulong)packed >> 32));
        return denominator == 0 ? numerator : (double)numerator / denominator;
    }

    private static int? TryGetInt(BitmapMetadata metadata, string query)
    {
        var value = TryGetDouble(metadata, query);
        return value.HasValue ? (int)Math.Round(value.Value) : null;
    }

    internal static (int Width, int Height) ApplyOrientationToDimensions(int width, int height, int? orientation)
        => orientation is >= 5 and <= 8 ? (height, width) : (width, height);

    private static DateTime? TryGetDateTime(BitmapMetadata metadata, string query)
    {
        if (!metadata.ContainsQuery(query))
        {
            return null;
        }

        try
        {
            var raw = metadata.GetQuery(query) as string;
            if (raw is not null &&
                DateTime.TryParseExact(raw, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return parsed;
            }
        }
        catch (InvalidCastException)
        {
        }

        return null;
    }

    internal static string? GetExposureProgramLabel(int? code)
    {
        return code switch
        {
            1 => "Tryb ręczny",
            2 => "Program automatyczny",
            3 => "Priorytet przysłony",
            4 => "Priorytet migawki",
            5 => "Program kreatywny",
            6 => "Program sportowy",
            7 => "Program portretowy",
            8 => "Program krajobrazowy",
            _ => null
        };
    }
}
