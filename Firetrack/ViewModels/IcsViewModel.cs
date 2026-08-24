using Firetrack.Models;
using Firetrack.Services;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.IO;

namespace Firetrack.ViewModels
{
    public class IcsViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private readonly PdfGenerationService _pdfService;
        private EquipmentModel? _equipment;
        private UserModel? _officer;
        private UserModel? _issuer;
        private bool _isBusy;
        private string _statusMessage = string.Empty;

        public EquipmentModel Equipment
        {
            get => _equipment!;
            set { _equipment = value; OnPropertyChanged(); }
        }

        public UserModel Officer
        {
            get => _officer!;
            set { _officer = value; OnPropertyChanged(); }
        }

        public UserModel Issuer
        {
            get => _issuer!;
            set { _issuer = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public ICommand GenerateIcsCommand { get; }

        public IcsViewModel(EquipmentModel? equipment, UserModel? officer)
        {
            _db = App.Database!;
            _pdfService = new PdfGenerationService();

            // ---- Equipment fallback ----
            if (equipment == null)
            {
                Equipment = new EquipmentModel
                {
                    Name = "Unknown Equipment",
                    QRCode = "N/A",
                    Type = "N/A"
                };
            }
            else
            {
                Equipment = equipment;
                if (string.IsNullOrEmpty(Equipment.Name))
                    Equipment.Name = "Unknown Equipment";
                if (string.IsNullOrEmpty(Equipment.QRCode))
                    Equipment.QRCode = "N/A";
                if (string.IsNullOrEmpty(Equipment.Type))
                    Equipment.Type = "N/A";
            }

            // ---- Officer fallback ----
            if (officer == null)
            {
                Officer = new UserModel
                {
                    FirstName = "Unknown",
                    LastName = "Officer",
                    Username = "N/A",
                    Role = "N/A"
                };
            }
            else
            {
                Officer = officer;
                // Instead of FullName, set FirstName/LastName
                if (string.IsNullOrEmpty(Officer.FirstName))
                    Officer.FirstName = "Unknown";
                if (string.IsNullOrEmpty(Officer.LastName))
                    Officer.LastName = "Officer";
                if (string.IsNullOrEmpty(Officer.Username))
                    Officer.Username = "N/A";
                if (string.IsNullOrEmpty(Officer.Role))
                    Officer.Role = "N/A";
            }

            // ---- Issuer fallback (current user or system) ----
            Issuer = App.CurrentUser ?? new UserModel
            {
                FirstName = "System",
                LastName = "",
                Role = "Admin"
            };
            if (string.IsNullOrEmpty(Issuer.FirstName))
                Issuer.FirstName = "System";
            if (string.IsNullOrEmpty(Issuer.Role))
                Issuer.Role = "Admin";

            GenerateIcsCommand = new Command(OnGenerateIcs);
        }

        private async void OnGenerateIcs()
        {
            IsBusy = true;
            StatusMessage = string.Empty;

            try
            {
                System.Diagnostics.Debug.WriteLine("📄 Generating ICS PDF...");
                var pdfBytes = _pdfService.GenerateIcsPdf(Equipment, Officer, Issuer);

                // ✅ Null check for PDF bytes
                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    StatusMessage = "❌ PDF generation returned empty data.";
                    IsBusy = false;
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"✅ PDF generated, size: {pdfBytes.Length} bytes");

                var fileName = $"ICS_{Equipment.QRCode}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                var downloadsPath = Path.Combine(FileSystem.AppDataDirectory, "ICS");
                Directory.CreateDirectory(downloadsPath);
                var filePath = Path.Combine(downloadsPath, fileName);
                await File.WriteAllBytesAsync(filePath, pdfBytes);
                System.Diagnostics.Debug.WriteLine($"✅ File saved: {filePath}");

                // Verify file exists
                if (!File.Exists(filePath))
                {
                    StatusMessage = "❌ PDF file was not created.";
                    IsBusy = false;
                    return;
                }

                // Open with fallback
                try
                {
                    await Launcher.Default.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(filePath) });
                }
                catch
                {
                    await Share.Default.RequestAsync(new ShareFileRequest
                    {
                        Title = "View ICS PDF",
                        File = new ShareFile(filePath)
                    });
                }

                StatusMessage = "✅ ICS generated and opened successfully.";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ PDF error: {ex}");
                StatusMessage = $"❌ Failed to generate PDF: {ex.Message}";
                await Shell.Current.DisplayAlert("Error", $"Failed to generate PDF: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}