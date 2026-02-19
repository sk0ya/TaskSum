using System.Globalization;
using System.Windows.Data;

namespace TaskSum.Converters;

[ValueConversion(typeof(double?), typeof(string))]
public class DoubleFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d) return d.ToString("0.##");
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
