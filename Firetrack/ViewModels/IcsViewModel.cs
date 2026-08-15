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

        public ICommand GenerateIcsCommand { get; }
        // GoBackCommand removed – navigation handled by Shell

        public IcsViewModel(EquipmentModel? equipment, UserModel? officer)
        {
            _db = App.Database!;
            _pdfService = new PdfGenerationService();

            // Equipment fallback
            if (equipment == null)
            {
                Equipment = new EquipmentModel { Name = "Unknown Equipment", QRCode = "N/A", Type = "N/A" };
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

            // Officer fallback
            if (officer == null)
            {
                Officer = new UserModel { FullName = "Unknown Officer", Username = "N/A", Role = "N/A" };
            }
            else
            {
                Officer = officer;
                if (string.IsNullOrEmpty(Officer.FullName))
                    Officer.FullName = "Unknown Officer";
                if (string.IsNullOrEmpty(Officer.Username))
                    Officer.Username = "N/A";
                if (string.IsNullOrEmpty(Officer.Role))
                    Officer.Role = "N/A";
            }

            Issuer = App.CurrentUser ?? new UserModel { FullName = "System", Role = "Admin" };
            if (string.IsNullOrEmpty(Issuer.FullName))
                Issuer.FullName = "System";
            if (string.IsNullOrEmpty(Issuer.Role))
                Issuer.Role = "Admin";

            GenerateIcsCommand = new Command(OnGenerateIcs);
            // GoBackCommand assignment removed
        }

        private async void OnGenerateIcs()
        {
            IsBusy = true;
            try
            {
                var pdfBytes = _pdfService.GenerateIcsPdf(Equipment, Officer, Issuer);

                var fileName = $"ICS_{Equipment.QRCode}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                var downloadsPath = Path.Combine(FileSystem.AppDataDirectory, "ICS");
                if (!Directory.Exists(downloadsPath))
                    Directory.CreateDirectory(downloadsPath);

                var filePath = Path.Combine(downloadsPath, fileName);
                await File.WriteAllBytesAsync(filePath, pdfBytes);

                await Launcher.Default.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(filePath)
                });

                await Shell.Current.DisplayAlert("Success", $"ICS saved to:\n{filePath}", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Failed to generate PDF: {ex.Message}", "OK");
                System.Diagnostics.Debug.WriteLine($"❌ PDF error: {ex}");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}