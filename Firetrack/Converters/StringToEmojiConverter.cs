using System.Globalization;
using Microsoft.Maui.Controls;

namespace Firetrack.Converters
{
    public class StringToEmojiConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string type)
            {
                return type.ToLowerInvariant() switch
                {
                    "hose" => "🌊",
                    "nozzle" => "💧",
                    "rescue tool" => "🛠️",
                    _ => "📦"
                };
            }
            return "📦";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}