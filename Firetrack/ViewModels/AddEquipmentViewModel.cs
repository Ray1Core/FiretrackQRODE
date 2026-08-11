using Firetrack.Models;
using Firetrack.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;

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

        public ObservableCollection<string> Types { get; } = new() { "Hose", "Nozzle", "Rescue Tool" };
        public ObservableCollection<string> Statuses { get; } = new() { "Available", "Issued", "Damaged", "InRepair" };

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

        public ICommand SaveCommand { get; }

        public AddEquipmentViewModel()
        {
            _db = App.Database!;
            SaveCommand = new Command(OnSave);
        }

        private async void OnSave()
        {
            if (string.IsNullOrWhiteSpace(QRCode) || string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Type))
            {
                await Shell.Current.DisplayAlert("Validation", "QR Code, Name, and Type are required.", "OK");
                return;
            }

            IsBusy = true;

            try
            {
                var existing = await _db.GetEquipmentByQRAsync(QRCode.Trim());
                if (existing != null)
                {
                    await Shell.Current.DisplayAlert("Error", "QR Code already exists.", "OK");
                    IsBusy = false;
                    return;
                }

                var newEquipment = new EquipmentModel
                {
                    QRCode = QRCode.Trim(),
                    Name = Name.Trim(),
                    Type = Type.Trim(),
                    Status = Status,
                    AssignedToUsername = null,
                    LastUpdated = DateTime.Now
                };

                await _db.SaveEquipmentAsync(newEquipment);

                await Shell.Current.DisplayAlert("Success", $"Equipment '{newEquipment.Name}' added successfully!", "OK");

                // Clear fields
                QRCode = string.Empty;
                Name = string.Empty;
                Type = string.Empty;
                Status = "Available";

                // Navigate back to InventoryPage
                await Shell.Current.GoToAsync("//InventoryPage");
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