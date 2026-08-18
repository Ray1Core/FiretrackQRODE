using Firetrack.Models;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;
using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Firetrack.Services
{
    public class PdfGenerationService
    {
        // ✅ No static constructor needed – resolver is set in MauiProgram.cs

        public byte[] GenerateIcsPdf(EquipmentModel equipment, UserModel officer, UserModel issuer)
        {
            try
            {
                using var document = new PdfDocument();
                var page = document.AddPage();
                var gfx = XGraphics.FromPdfPage(page);

                var titleFont = new XFont("Helvetica-Bold", 18);
                var headerFont = new XFont("Helvetica-Bold", 12);
                var bodyFont = new XFont("Helvetica", 10);
                var labelFont = new XFont("Helvetica-Bold", 10);

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

                DrawRow("QR Code:", equipment.QRCode ?? "N/A");
                DrawRow("Equipment Name:", equipment.Name ?? "N/A");
                DrawRow("Type:", equipment.Type ?? "N/A");
                DrawRow("Status:", equipment.Status ?? "N/A");
                yPos += 8;

                // ---- Custodian Details ----
                gfx.DrawString("CUSTODIAN INFORMATION", headerFont, XBrushes.Black,
                    new XRect(leftMargin, yPos, pageWidth - leftMargin - rightMargin, 20), XStringFormats.TopLeft);
                yPos += 25;

                DrawRow("Custodian Name:", officer?.FullName ?? "N/A");
                DrawRow("Officer ID:", officer?.Username ?? "N/A");
                DrawRow("Role:", officer?.Role ?? "N/A");
                yPos += 8;

                // ---- Issuing Officer ----
                gfx.DrawString("ISSUING OFFICER", headerFont, XBrushes.Black,
                    new XRect(leftMargin, yPos, pageWidth - leftMargin - rightMargin, 20), XStringFormats.TopLeft);
                yPos += 25;

                DrawRow($"Name: {issuer?.FullName ?? "N/A"}", "");
                DrawRow($"Position: {issuer?.Role ?? "N/A"}", "");
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ PDF generation failed: {ex}");
                throw new Exception($"PDF generation failed: {ex.Message}", ex);
            }
        }

        public byte[] GenerateClearanceCertificate(UserModel officer, List<EquipmentModel> equipment)
        {
            try
            {
                using var document = new PdfDocument();
                var page = document.AddPage();
                var gfx = XGraphics.FromPdfPage(page);

                var titleFont = new XFont("Helvetica-Bold", 20);
                var headerFont = new XFont("Helvetica-Bold", 14);
                var bodyFont = new XFont("Helvetica", 10);

                double yPos = 40;
                const double leftMargin = 50;
                const double rightMargin = 50;
                double pageWidth = page.Width;

                // Header
                gfx.DrawString("BUREAU OF FIRE PROTECTION", titleFont, XBrushes.Black,
                    new XRect(0, yPos, pageWidth, 30), XStringFormats.TopCenter);
                yPos += 35;

                gfx.DrawString("CEBU CITY FIRE STATION", headerFont, XBrushes.Black,
                    new XRect(0, yPos, pageWidth, 25), XStringFormats.TopCenter);
                yPos += 35;

                gfx.DrawString("CLEARANCE CERTIFICATE", titleFont, XBrushes.Black,
                    new XRect(0, yPos, pageWidth, 30), XStringFormats.TopCenter);
                yPos += 40;

                // Officer details
                gfx.DrawString($"This is to certify that {officer.FullName}", bodyFont, XBrushes.Black,
                    new XRect(leftMargin, yPos, pageWidth - leftMargin - rightMargin, 20), XStringFormats.TopLeft);
                yPos += 25;
                gfx.DrawString($"({officer.Username}) has no outstanding equipment accountability.", bodyFont, XBrushes.Black,
                    new XRect(leftMargin, yPos, pageWidth - leftMargin - rightMargin, 20), XStringFormats.TopLeft);
                yPos += 35;

                // Equipment list
                if (equipment.Any())
                {
                    gfx.DrawString("All equipment has been returned:", headerFont, XBrushes.Black,
                        new XRect(leftMargin, yPos, pageWidth - leftMargin - rightMargin, 20), XStringFormats.TopLeft);
                    yPos += 25;

                    foreach (var eq in equipment)
                    {
                        gfx.DrawString($"• {eq.Name} ({eq.QRCode})", bodyFont, XBrushes.Black,
                            new XRect(leftMargin + 20, yPos, pageWidth - leftMargin - rightMargin - 20, 18), XStringFormats.TopLeft);
                        yPos += 20;
                    }
                }
                else
                {
                    gfx.DrawString("No equipment was ever assigned to this officer.", bodyFont, XBrushes.Black,
                        new XRect(leftMargin, yPos, pageWidth - leftMargin - rightMargin, 20), XStringFormats.TopLeft);
                    yPos += 25;
                }

                yPos += 20;

                // Signatures
                gfx.DrawLine(XPens.Black, leftMargin, yPos, leftMargin + 200, yPos);
                gfx.DrawString("Clearance Officer", bodyFont, XBrushes.Black,
                    new XRect(leftMargin, yPos + 5, 200, 15), XStringFormats.TopCenter);

                gfx.DrawLine(XPens.Black, pageWidth - rightMargin - 200, yPos, pageWidth - rightMargin, yPos);
                gfx.DrawString("Officer Signature", bodyFont, XBrushes.Black,
                    new XRect(pageWidth - rightMargin - 200, yPos + 5, 200, 15), XStringFormats.TopCenter);

                // Footer
                yPos = page.Height - 40;
                gfx.DrawLine(XPens.Black, leftMargin, yPos, pageWidth - rightMargin, yPos);
                yPos += 15;
                gfx.DrawString($"Issued: {DateTime.Now:MMMM dd, yyyy}", bodyFont, XBrushes.Black,
                    new XRect(0, yPos, pageWidth, 15), XStringFormats.TopCenter);

                using var stream = new MemoryStream();
                document.Save(stream, false);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Clearance PDF generation failed: {ex}");
                throw new Exception($"Clearance PDF generation failed: {ex.Message}", ex);
            }
        }

        public byte[] GenerateDisposalCertificate(EquipmentModel equipment, UserModel approvedBy, string remarks = "")
        {
            try
            {
                using var document = new PdfDocument();
                var page = document.AddPage();
                var gfx = XGraphics.FromPdfPage(page);

                var titleFont = new XFont("Helvetica-Bold", 18);
                var headerFont = new XFont("Helvetica-Bold", 12);
                var bodyFont = new XFont("Helvetica", 10);

                double yPos = 40;
                const double leftMargin = 50;
                const double rightMargin = 50;
                double pageWidth = page.Width;

                // Header
                gfx.DrawString("BUREAU OF FIRE PROTECTION", titleFont, XBrushes.Black,
                    new XRect(0, yPos, pageWidth, 30), XStringFormats.TopCenter);
                yPos += 35;

                gfx.DrawString("CEBU CITY FIRE STATION", headerFont, XBrushes.Black,
                    new XRect(0, yPos, pageWidth, 25), XStringFormats.TopCenter);
                yPos += 35;

                gfx.DrawString("DISPOSAL CERTIFICATE", titleFont, XBrushes.Black,
                    new XRect(0, yPos, pageWidth, 30), XStringFormats.TopCenter);
                yPos += 40;

                // Details
                gfx.DrawString($"Equipment Name: {equipment.Name}", bodyFont, XBrushes.Black,
                    new XRect(leftMargin, yPos, pageWidth - leftMargin - rightMargin, 20), XStringFormats.TopLeft);
                yPos += 25;
                gfx.DrawString($"QR Code: {equipment.QRCode}", bodyFont, XBrushes.Black,
                    new XRect(leftMargin, yPos, pageWidth - leftMargin - rightMargin, 20), XStringFormats.TopLeft);
                yPos += 25;
                gfx.DrawString($"Type: {equipment.Type}", bodyFont, XBrushes.Black,
                    new XRect(leftMargin, yPos, pageWidth - leftMargin - rightMargin, 20), XStringFormats.TopLeft);
                yPos += 25;
                gfx.DrawString($"Reason for Disposal: {equipment.DisposalReason ?? "N/A"}", bodyFont, XBrushes.Black,
                    new XRect(leftMargin, yPos, pageWidth - leftMargin - rightMargin, 20), XStringFormats.TopLeft);
                yPos += 25;
                gfx.DrawString($"Requested by: {equipment.DisposalRequestedBy ?? "N/A"}", bodyFont, XBrushes.Black,
                    new XRect(leftMargin, yPos, pageWidth - leftMargin - rightMargin, 20), XStringFormats.TopLeft);
                yPos += 25;
                gfx.DrawString($"Requested on: {equipment.DisposalRequestDate?.ToString("MMMM dd, yyyy") ?? "N/A"}", bodyFont, XBrushes.Black,
                    new XRect(leftMargin, yPos, pageWidth - leftMargin - rightMargin, 20), XStringFormats.TopLeft);
                yPos += 25;
                gfx.DrawString($"Approved by: {approvedBy.FullName} ({approvedBy.Username})", bodyFont, XBrushes.Black,
                    new XRect(leftMargin, yPos, pageWidth - leftMargin - rightMargin, 20), XStringFormats.TopLeft);
                yPos += 25;
                gfx.DrawString($"Approved on: {DateTime.Now:MMMM dd, yyyy}", bodyFont, XBrushes.Black,
                    new XRect(leftMargin, yPos, pageWidth - leftMargin - rightMargin, 20), XStringFormats.TopLeft);
                yPos += 25;
                if (!string.IsNullOrEmpty(remarks))
                {
                    gfx.DrawString($"Remarks: {remarks}", bodyFont, XBrushes.Black,
                        new XRect(leftMargin, yPos, pageWidth - leftMargin - rightMargin, 20), XStringFormats.TopLeft);
                    yPos += 25;
                }

                yPos += 20;

                // Signatures
                gfx.DrawLine(XPens.Black, leftMargin, yPos, leftMargin + 200, yPos);
                gfx.DrawString("Disposal Officer", bodyFont, XBrushes.Black,
                    new XRect(leftMargin, yPos + 5, 200, 15), XStringFormats.TopCenter);

                gfx.DrawLine(XPens.Black, pageWidth - rightMargin - 200, yPos, pageWidth - rightMargin, yPos);
                gfx.DrawString("Authorized Signature", bodyFont, XBrushes.Black,
                    new XRect(pageWidth - rightMargin - 200, yPos + 5, 200, 15), XStringFormats.TopCenter);

                // Footer
                yPos = page.Height - 40;
                gfx.DrawLine(XPens.Black, leftMargin, yPos, pageWidth - rightMargin, yPos);
                yPos += 15;
                gfx.DrawString("This equipment is officially disposed and removed from active inventory.",
                    bodyFont, XBrushes.Black,
                    new XRect(0, yPos, pageWidth, 15), XStringFormats.TopCenter);

                using var stream = new MemoryStream();
                document.Save(stream, false);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Disposal PDF generation failed: {ex}");
                throw new Exception($"Disposal PDF generation failed: {ex.Message}", ex);
            }
        }
    }
}