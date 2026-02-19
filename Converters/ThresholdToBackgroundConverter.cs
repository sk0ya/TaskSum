using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TaskSum.Converters;

/// <summary>
/// double 値がしきい値（Parameter、省略時 20）を超えた場合に赤ブラシを返す。
/// </summary>
[ValueConversion(typeof(double), typeof(Brush))]
public class ThresholdToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double threshold = 20.0;
        if (parameter is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
            threshold = parsed;

        if (value is double d && d > threshold)
            return new SolidColorBrush(Color.FromRgb(255, 153, 153)); // 薄い赤

        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
