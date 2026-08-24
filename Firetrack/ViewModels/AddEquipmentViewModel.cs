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
        private string _name = string.Empty;
        private string _type = string.Empty;
        private string _status = "Available";
        private bool _isBusy;

        public ObservableCollection<string> Types { get; } = new() { "Hose", "Nozzle", "Rescue Tool" };
        public ObservableCollection<string> Statuses { get; } = new() { "Available", "Issued", "Damaged", "InRepair", "Disposed" };

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
            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Type))
            {
                await Shell.Current.DisplayAlert("Validation", "Please fill in Name and Type.", "OK");
                return;
            }

            IsBusy = true;

            try
            {
                // Generate unique QR: NAME_YYYYMMDDHHMMSS_GUID(8)
                string uniqueSuffix = Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
                string qrValue = $"{Name.Trim().ToUpper()}_{DateTime.Now:yyyyMMddHHmmss}_{uniqueSuffix}";

                var existing = await _db.GetEquipmentByQRAsync(qrValue);
                if (existing != null)
                {
                    await Shell.Current.DisplayAlert("Error", "QR Code collision – please try again.", "OK");
                    IsBusy = false;
                    return;
                }

                var newEquipment = new EquipmentModel
                {
                    QRCode = qrValue,
                    Name = Name.Trim(),
                    Type = Type.Trim(),
                    Status = Status,
                    AssignedToUsername = null,
                    LastUpdated = DateTime.Now,
                    IsDisposalRequested = false
                };

                await _db.SaveEquipmentAsync(newEquipment);

                if (App.CurrentUser != null)
                {
                    await _db.LogActionAsync(
                        App.CurrentUser.Username,
                        "Add Equipment",
                        $"Added '{newEquipment.Name}' ({newEquipment.QRCode})");
                }

                await Shell.Current.DisplayAlert("Success", $"Equipment '{newEquipment.Name}' added successfully!\nQR: {newEquipment.QRCode}", "OK");

                Name = string.Empty;
                Type = string.Empty;
                Status = "Available";

                // ✅ Absolute navigation
                await Shell.Current.GoToAsync(".."); // Goes back to the previous page (Inventory)
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