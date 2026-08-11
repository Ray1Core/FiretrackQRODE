using System.Globalization;

namespace Firetrack.Converters
{
    public class StringToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string text && !string.IsNullOrEmpty(text))
            {
                return text.StartsWith("✅") || text.StartsWith("Success") ? Colors.Green :
                       text.StartsWith("❌") || text.StartsWith("Error") ? Colors.Red :
                       Colors.Orange;
            }
            return Colors.Gray;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}