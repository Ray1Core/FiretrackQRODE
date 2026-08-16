using Firetrack.Models;
using Firetrack.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using QRCoder;
using System.IO;

namespace Firetrack.ViewModels
{
    public class AddEquipmentViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private string _qrCode = string.Empty;
        private string _name = string.Empty;
        private string _type = string.Empty;
        private string _status = "Available";
        private bool _isBusy;
        private ImageSource? _qrPreview;

        public ObservableCollection<string> Types { get; } = new() { "Hose", "Nozzle", "Rescue Tool" };
        public ObservableCollection<string> Statuses { get; } = new() { "Available", "Issued", "Damaged", "InRepair", "Disposed" };

        public string QRCode
        {
            get => _qrCode;
            set { _qrCode = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ImageSource? QRPreview
        {
            get => _qrPreview;
            set { _qrPreview = value; OnPropertyChanged(); }
        }

        public ICommand SaveCommand { get; }
        public ICommand GenerateQRCommand { get; }

        public AddEquipmentViewModel()
        {
            _db = App.Database!;
            SaveCommand = new Command(OnSave);
            GenerateQRCommand = new Command(OnGenerateQR);
        }

        private void OnGenerateQR()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                Shell.Current.DisplayAlert("Validation", "Enter equipment name first to generate QR.", "OK");
                return;
            }

            try
            {
                // Build QR value: Name + Timestamp (ensures uniqueness)
                string qrValue = $"{Name.Trim().ToUpper()}_{DateTime.Now:yyyyMMddHHmmss}";
                QRCode = qrValue;

                var generator = new QRCodeGenerator();
                var qrCodeData = generator.CreateQrCode(qrValue, QRCodeGenerator.ECCLevel.Q);
                var qrCode = new PngByteQRCode(qrCodeData);
                var pngBytes = qrCode.GetGraphic(20);

                QRPreview = ImageSource.FromStream(() => new MemoryStream(pngBytes));
            }
            catch (Exception ex)
            {
                Shell.Current.DisplayAlert("Error", $"QR generation failed: {ex.Message}", "OK");
            }
        }

        private async void OnSave()
        {
            if (string.IsNullOrWhiteSpace(QRCode) || string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Type))
            {
                await Shell.Current.DisplayAlert("Validation", "Generate QR first, then fill all fields.", "OK");
                return;
            }

            IsBusy = true;

            try
            {
                var existing = await _db.GetEquipmentByQRAsync(QRCode);
                if (existing != null)
                {
                    await Shell.Current.DisplayAlert("Error", "QR Code already exists.", "OK");
                    IsBusy = false;
                    return;
                }

                var newEquipment = new EquipmentModel
                {
                    QRCode = QRCode,
                    Name = Name.Trim(),
                    Type = Type.Trim(),
                    Status = Status,
                    AssignedToUsername = null,
                    LastUpdated = DateTime.Now
                };

                await _db.SaveEquipmentAsync(newEquipment);

                if (App.CurrentUser != null)
                {
                    await _db.LogActionAsync(
                        App.CurrentUser.Username,
                        "Add Equipment",
                        $"Added '{newEquipment.Name}' ({newEquipment.QRCode})");
                }

                await Shell.Current.DisplayAlert("Success", $"Equipment '{newEquipment.Name}' added successfully!", "OK");

                // Reset fields
                QRCode = string.Empty;
                Name = string.Empty;
                Type = string.Empty;
                Status = "Available";
                QRPreview = null;

                await Shell.Current.GoToAsync("//EquipmentCategoryPage");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}