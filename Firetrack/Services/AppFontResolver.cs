using PdfSharpCore.Fonts;
using Microsoft.Maui.Storage;
using System.IO;

namespace Firetrack.Services
{
    public class AppFontResolver : IFontResolver
    {
        // ---- Required by IFontResolver ----
        public string DefaultFontName => "OpenSans-Regular.ttf";

        public byte[]? GetFont(string faceName)
        {
            // Only handle our known fonts
            if (string.IsNullOrEmpty(faceName))
                return null;

            if (!faceName.Contains("OpenSans") &&
                faceName != "Arial" &&
                faceName != "Helvetica" &&
                faceName != "sans-serif")
                return null;

            try
            {
                // Open the font from the app package (it's copied via MauiFont)
                using var stream = FileSystem.OpenAppPackageFileAsync("OpenSans-Regular.ttf").GetAwaiter().GetResult();
                if (stream != null)
                {
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            catch
            {
                // fallback to null
            }
            return null;
        }

        public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
        {
            // Map common font families to our embedded OpenSans
            if (string.IsNullOrEmpty(familyName))
                return null;

            if (familyName == "OpenSans" ||
                familyName == "Arial" ||
                familyName == "Helvetica" ||
                familyName == "sans-serif")
            {
                return new FontResolverInfo("OpenSans-Regular.ttf");
            }
            return null;
        }
    }
}