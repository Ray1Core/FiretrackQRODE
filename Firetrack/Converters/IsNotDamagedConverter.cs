using System.Globalization;
using Microsoft.Maui.Controls;

namespace Firetrack.Converters
{
    public class IsNotDamagedConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string status)
                return status != "Damaged";
            return true;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}