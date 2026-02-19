using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TaskSum.Converters;

[ValueConversion(typeof(int), typeof(Thickness))]
public class LevelToIndentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int level = value is int l ? l : 0;
        return new Thickness(level * 16, 0, 0, 0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
