using PdfSharpCore.Fonts;
using System;

namespace Firetrack.Services
{
    public class PdfFontResolver : IFontResolver
    {
        // REQUIRED: Default fallback font
        public string DefaultFontName => "Helvetica";

        public byte[]? GetFont(string faceName)
        {
            // We don't need to return custom font data – we use built‑in PDF fonts
            return null;
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic)
        {
            // Map any requested font to a standard PDF font
            if (familyName.Contains("Helvetica", StringComparison.OrdinalIgnoreCase))
                return new FontResolverInfo("Helvetica");
            if (familyName.Contains("Times", StringComparison.OrdinalIgnoreCase))
                return new FontResolverInfo("TimesRoman");
            if (familyName.Contains("Courier", StringComparison.OrdinalIgnoreCase))
                return new FontResolverInfo("Courier");

            // Fallback to Helvetica
            return new FontResolverInfo("Helvetica");
        }
    }
}