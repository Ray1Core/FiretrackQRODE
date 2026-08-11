using System.Globalization;

namespace Firetrack.Converters
{
    public class StatusColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string status && !string.IsNullOrEmpty(status))
            {
                return status.ToLower() switch
                {
                    "available" => Colors.Green,
                    "issued" => Colors.Orange,
                    "damaged" => Colors.Red,
                    "inrepair" => Colors.Blue,
                    _ => Colors.Gray
                };
            }
            return Colors.Gray;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}