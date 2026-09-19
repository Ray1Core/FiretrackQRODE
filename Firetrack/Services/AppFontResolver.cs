using PdfSharpCore.Fonts;
using System;
using System.IO;
using System.Reflection;

namespace Firetrack.Services
{
    public class AppFontResolver : IFontResolver
    {
        // Required by IFontResolver
        public string DefaultFontName => "OpenSans-Regular.ttf";

        public byte[]? GetFont(string faceName)
        {
            if (string.IsNullOrEmpty(faceName))
                return null;

            // Normalize to lower case for comparison
            string lowerFace = faceName.ToLowerInvariant();

            // Decide which embedded resource to load
            string resourceName;
            if (lowerFace.Contains("semibold") || lowerFace.Contains("bold"))
                resourceName = "Firetrack.Resources.Fonts.OpenSans-Semibold.ttf";
            else
                resourceName = "Firetrack.Resources.Fonts.OpenSans-Regular.ttf";

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream(resourceName);

                if (stream == null)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Font resource not found: {resourceName}");
                    // Fallback: try to list all resources for debugging
                    foreach (var name in assembly.GetManifestResourceNames())
                        System.Diagnostics.Debug.WriteLine($"   Available resource: {name}");
                    return null;
                }

                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                return ms.ToArray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error loading font {faceName}: {ex.Message}");
                return null;
            }
        }

        public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
        {
            if (string.IsNullOrEmpty(familyName))
                return null;

            // Map any known family to OpenSans.
            // We ignore italic for now — PdfSharpCore will simulate it if needed.
            string faceName = bold
                ? "OpenSans-Semibold.ttf"
                : "OpenSans-Regular.ttf";

            return new FontResolverInfo(faceName);
        }
    }
}