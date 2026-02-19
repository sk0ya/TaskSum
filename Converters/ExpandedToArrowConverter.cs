using System.Globalization;
using System.Windows.Data;

namespace TaskSum.Converters;

[ValueConversion(typeof(bool), typeof(string))]
public class ExpandedToArrowConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? "▼" : "▶";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
