using PdfSharpCore.Fonts;
using System;

namespace Firetrack.Services
{
    public class PdfFontResolver : IFontResolver
    {
        public string DefaultFontName => "Helvetica";

        public byte[]? GetFont(string faceName)
        {
            // No custom font data – use built‑in PDF fonts
            return null;
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic)
        {
            // Map the requested font family to one of the standard PDF fonts.
            // The built‑in fonts support bold/italic via separate names.

            // Helvetica family
            if (familyName.Contains("Helvetica", StringComparison.OrdinalIgnoreCase))
            {
                if (bold && italic)
                    return new FontResolverInfo("Helvetica-BoldOblique");
                if (bold)
                    return new FontResolverInfo("Helvetica-Bold");
                if (italic)
                    return new FontResolverInfo("Helvetica-Oblique");
                return new FontResolverInfo("Helvetica");
            }

            // Times / TimesRoman family
            if (familyName.Contains("Times", StringComparison.OrdinalIgnoreCase))
            {
                if (bold && italic)
                    return new FontResolverInfo("Times-BoldItalic");
                if (bold)
                    return new FontResolverInfo("Times-Bold");
                if (italic)
                    return new FontResolverInfo("Times-Italic");
                return new FontResolverInfo("TimesRoman");
            }

            // Courier family
            if (familyName.Contains("Courier", StringComparison.OrdinalIgnoreCase))
            {
                if (bold && italic)
                    return new FontResolverInfo("Courier-BoldOblique");
                if (bold)
                    return new FontResolverInfo("Courier-Bold");
                if (italic)
                    return new FontResolverInfo("Courier-Oblique");
                return new FontResolverInfo("Courier");
            }

            // Fallback – use Helvetica with bold/italic if requested
            if (bold && italic)
                return new FontResolverInfo("Helvetica-BoldOblique");
            if (bold)
                return new FontResolverInfo("Helvetica-Bold");
            if (italic)
                return new FontResolverInfo("Helvetica-Oblique");

            return new FontResolverInfo("Helvetica");
        }
    }
}