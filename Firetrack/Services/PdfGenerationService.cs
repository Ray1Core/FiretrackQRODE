using Firetrack.Models;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;
using System.IO;

namespace Firetrack.Services
{
    public class PdfGenerationService
    {
        public byte[] GenerateIcsPdf(EquipmentModel equipment, UserModel officer, UserModel issuer)
        {
            using var document = new PdfDocument();
            var page = document.AddPage();
            var gfx = XGraphics.FromPdfPage(page);

            var titleFont = new XFont("Arial", 18, XFontStyle.Bold);
            var headerFont = new XFont("Arial", 12, XFontStyle.Bold);
            var bodyFont = new XFont("Arial", 10, XFontStyle.Regular);
            var labelFont = new XFont("Arial", 10, XFontStyle.Bold);

            double yPos = 40;
            const double leftMargin = 50;
            const double rightMargin = 50;
            double pageWidth = page.Width;

            // ---- Header ----
            gfx.DrawString("BUREAU OF FIRE PROTECTION", titleFont, XBrushes.Black,
                new XRect(0, yPos, pageWidth, 30), XStringFormats.TopCenter);
            yPos += 35;

            gfx.DrawString("CEBU CITY FIRE STATION", headerFont, XBrushes.Black,
                new XRect(0, yPos, pageWidth, 25), XStringFormats.TopCenter);
            yPos += 30;

            gfx.DrawString("INVENTORY CUSTODIAN SLIP (ICS)", headerFont, XBrushes.Black,
                new XRect(0, yPos, pageWidth, 25), XStringFormats.TopCenter);
            yPos += 35;

            gfx.DrawLine(XPens.Black, leftMargin, yPos, pageWidth - rightMargin, yPos);
            yPos += 20;

            // ---- ICS Number and Date ----
            var icsNumber = $"ICS-{DateTime.Now:yyyyMMdd}-{equipment.EquipmentId:D4}";
            gfx.DrawString($"ICS No.: {icsNumber}", bodyFont, XBrushes.Black,
                new XRect(leftMargin, yPos, pageWidth - leftMargin - rightMargin, 20), XStringFormats.TopLeft);
            yPos += 25;

            gfx.DrawString($"Date Issued: {DateTime.Now:MMMM dd, yyyy}", bodyFont, XBrushes.Black,
                new XRect(leftMargin, yPos, pageWidth - leftMargin - rightMargin, 20), XStringFormats.TopLeft);
            yPos += 30;

            // ---- Equipment Details ----
            gfx.DrawString("EQUIPMENT DETAILS", headerFont, XBrushes.Black,
                new XRect(leftMargin, yPos, pageWidth - leftMargin - rightMargin, 20), XStringFormats.TopLeft);
            yPos += 25;

            double col1Width = 120;
            double col2Width = pageWidth - leftMargin - rightMargin - col1Width - 10;

            void DrawRow(string label, string value)
            {
                gfx.DrawString(label, labelFont, XBrushes.Black,
                    new XRect(leftMargin, yPos, col1Width, 20), XStringFormats.TopLeft);
                gfx.DrawString(value, bodyFont, XBrushes.Black,
                    new XRect(leftMargin + col1Width + 10, yPos, col2Width, 20), XStringFormats.TopLeft);
                yPos += 22;
            }

            DrawRow("QR Code:", equipment.QRCode);
            DrawRow("Equipment Name:", equipment.Name);
            DrawRow("Type:", equipment.Type);
            DrawRow("Status:", equipment.Status);
            yPos += 8;

            // ---- Custodian Details ----
            gfx.DrawString("CUSTODIAN INFORMATION", headerFont, XBrushes.Black,
                new XRect(leftMargin, yPos, pageWidth - leftMargin - rightMargin, 20), XStringFormats.TopLeft);
            yPos += 25;

            DrawRow("Custodian Name:", officer.FullName);
            DrawRow("Officer ID:", officer.Username);
            DrawRow("Role:", officer.Role);
            yPos += 8;

            // ---- Issuing Officer ----
            gfx.DrawString("ISSUING OFFICER", headerFont, XBrushes.Black,
                new XRect(leftMargin, yPos, pageWidth - leftMargin - rightMargin, 20), XStringFormats.TopLeft);
            yPos += 25;

            DrawRow($"Name: {issuer.FullName}", "");
            DrawRow($"Position: {issuer.Role}", "");
            yPos += 10;

            // ---- Signatures ----
            gfx.DrawLine(XPens.Black, leftMargin, yPos, leftMargin + 150, yPos);
            gfx.DrawString("Custodian Signature", bodyFont, XBrushes.Black,
                new XRect(leftMargin, yPos + 5, 150, 15), XStringFormats.TopCenter);

            gfx.DrawLine(XPens.Black, pageWidth - rightMargin - 150, yPos, pageWidth - rightMargin, yPos);
            gfx.DrawString("Issuing Officer Signature", bodyFont, XBrushes.Black,
                new XRect(pageWidth - rightMargin - 150, yPos + 5, 150, 15), XStringFormats.TopCenter);

            // ---- Footer ----
            yPos = page.Height - 40;
            gfx.DrawLine(XPens.Black, leftMargin, yPos, pageWidth - rightMargin, yPos);
            yPos += 15;
            gfx.DrawString("This is a system-generated document. Wet signature required for official use.",
                bodyFont, XBrushes.Black,
                new XRect(0, yPos, pageWidth, 15), XStringFormats.TopCenter);

            using var stream = new MemoryStream();
            document.Save(stream, false);
            return stream.ToArray();
        }
    }
}