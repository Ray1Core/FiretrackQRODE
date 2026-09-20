using QRCoder;
using System;
using System.IO;

namespace Firetrack.Helpers
{
    public static class QrHelper
    {
        /// <summary>
        /// Generates a QR code PNG as a byte[] from the given text.
        /// </summary>
        public static byte[] GeneratePng(string text, int pixelsPerModule = 20)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<byte>();

            var generator = new QRCodeGenerator();
            var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            var png = new PngByteQRCode(data);
            return png.GetGraphic(pixelsPerModule);
        }

        /// <summary>
        /// Generates a QR code as an ImageSource ready for XAML binding.
        /// </summary>
        public static ImageSource? GenerateImageSource(string text, int pixelsPerModule = 20)
        {
            var bytes = GeneratePng(text, pixelsPerModule);
            if (bytes.Length == 0)
                return null;

            return ImageSource.FromStream(() => new MemoryStream(bytes));
        }
    }
}