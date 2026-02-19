using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TaskSum.Converters;

[ValueConversion(typeof(string), typeof(Brush))]
public class WorkItemTypeColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (value as string) switch
        {
            "Feature"               => new SolidColorBrush(Color.FromRgb(119, 59, 147)),
            "Epic"                  => new SolidColorBrush(Color.FromRgb(255, 123, 0)),
            "User Story"            => new SolidColorBrush(Color.FromRgb(0, 120, 212)),
            "Product Backlog Item"  => new SolidColorBrush(Color.FromRgb(0, 120, 212)),
            "Bug"                   => new SolidColorBrush(Color.FromRgb(204, 41, 61)),
            "Task"                  => new SolidColorBrush(Color.FromRgb(242, 203, 29)),
            _                       => new SolidColorBrush(Colors.Gray),
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
