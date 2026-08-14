using System.Globalization;
using Microsoft.Maui.Controls;

namespace Firetrack.Converters
{
    public class IsInventoryModeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is string mode && mode == "inventory";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}