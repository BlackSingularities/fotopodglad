using System.Globalization;
using System.Windows.Data;

namespace Fotopodglad.Converters;

[ValueConversion(typeof(long), typeof(string))]
public sealed class FileSizeToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long bytes || bytes <= 0)
        {
            return "—";
        }

        double size = bytes;
        string[] units = ["B", "KB", "MB", "GB"];
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size.ToString(unitIndex == 0 ? "0" : "0.#", CultureInfo.InvariantCulture)} {units[unitIndex]}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
