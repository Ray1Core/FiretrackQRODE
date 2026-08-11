using System.Globalization;

namespace Firetrack.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value switch
            {
                "Available" => Colors.Green,
                "Issued" => Colors.Orange,
                "Damaged" => Colors.Red,
                "InRepair" => Colors.Blue,
                _ => Colors.Gray
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}