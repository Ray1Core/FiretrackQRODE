using Firetrack.Models;
using Firetrack.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class TransferViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private EquipmentModel _selectedEquipment = null!;
        private UserModel _selectedPersonnel = null!;
        private string _scanStatus = "Scan equipment to begin.";
        private bool _isBusy;
        private ObservableCollection<UserModel> _personnelList = new();

        public EquipmentModel SelectedEquipment
        {
            get => _selectedEquipment;
            set { _selectedEquipment = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanTransfer)); }
        }

        public UserModel SelectedPersonnel
        {
            get => _selectedPersonnel;
            set { _selectedPersonnel = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanTransfer)); }
        }

        public ObservableCollection<UserModel> PersonnelList
        {
            get => _personnelList;
            set { _personnelList = value; OnPropertyChanged(); }
        }

        public string ScanStatus
        {
            get => _scanStatus;
            set { _scanStatus = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public bool CanTransfer => SelectedEquipment != null && SelectedPersonnel != null && SelectedEquipment.Status == "Available";

        public ICommand ScanEquipmentCommand { get; }
        public ICommand ConfirmTransferCommand { get; }
        public ICommand ResetCommand { get; }

        public TransferViewModel()
        {
            _db = App.Database!;
            ScanEquipmentCommand = new Command(OnScanEquipment);
            ConfirmTransferCommand = new Command(OnConfirmTransfer);
            ResetCommand = new Command(OnReset);

            LoadPersonnel();
        }

        private async void LoadPersonnel()
        {
            var users = await _db.GetUsersAsync();
            PersonnelList.Clear();
            foreach (var u in users.Where(u => u.Role == "Personnel" && u.IsActive))
                PersonnelList.Add(u);
        }

        // ---- Scan Equipment: Navigate to ScannerPage with return parameter ----
        private async void OnScanEquipment()
        {
            // Navigate to Scanner with a returnTo parameter
            await Shell.Current.GoToAsync($"ScannerPage?returnTo=TransferPage");
        }

        // ---- This method is called when we return from Scanner with a QR ----
        public async Task ProcessScannedQR(string qrCode)
        {
            if (string.IsNullOrWhiteSpace(qrCode))
                return;

            IsBusy = true;
            ScanStatus = "Processing QR...";

            try
            {
                var equipment = await _db.GetEquipmentByQRAsync(qrCode);
                if (equipment != null && equipment.Status == "Available")
                {
                    SelectedEquipment = equipment;
                    ScanStatus = $"✅ Equipment '{equipment.Name}' ready for transfer.";
                }
                else if (equipment != null && equipment.Status != "Available")
                {
                    ScanStatus = $"❌ Equipment '{equipment.Name}' is not available (Status: {equipment.Status}).";
                }
                else
                {
                    ScanStatus = "❌ Equipment not found or invalid QR.";
                }
            }
            catch (Exception ex)
            {
                ScanStatus = $"❌ Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ---- Confirm Transfer ----
        private async void OnConfirmTransfer()
        {
            if (!CanTransfer)
            {
                await Shell.Current.DisplayAlert("Validation", "Please scan equipment and select a personnel.", "OK");
                return;
            }

            bool confirm = await Shell.Current.DisplayAlert(
                "Confirm Transfer",
                $"Transfer '{SelectedEquipment.Name}' to {SelectedPersonnel.FullName}?",
                "Yes",
                "Cancel");

            if (!confirm) return;

            IsBusy = true;
            try
            {
                var admin = App.CurrentUser;
                if (admin == null)
                {
                    ScanStatus = "❌ Admin not logged in.";
                    IsBusy = false;
                    return;
                }

                // Record transaction
                var transaction = new TransactionModel
                {
                    EquipmentQR = SelectedEquipment.QRCode,
                    FromUser = admin.Username,
                    ToUser = SelectedPersonnel.Username,
                    Timestamp = DateTime.Now,
                    Action = "Issue",
                    Remarks = $"Issued by {admin.FullName} to {SelectedPersonnel.FullName}"
                };

                SelectedEquipment.AssignedToUsername = SelectedPersonnel.Username;
                SelectedEquipment.Status = "Issued";
                SelectedEquipment.LastUpdated = DateTime.Now;

                await _db.SaveTransactionAsync(transaction);
                await _db.SaveEquipmentAsync(SelectedEquipment);

                await _db.LogActionAsync(
                    admin.Username,
                    "Transfer",
                    $"Issued '{SelectedEquipment.Name}' to {SelectedPersonnel.Username}");

                await _db.SendNotificationAsync(
                    SelectedPersonnel.Username,
                    "🔄 Equipment Issued",
                    $"{admin.FullName} issued '{SelectedEquipment.Name}' to you.");

                // Navigate to ICS generation
                var navParams = new Dictionary<string, object>
                {
                    { "equipment", SelectedEquipment },
                    { "officer", SelectedPersonnel }
                };
                await Shell.Current.GoToAsync("IcsPage", navParams);

                ScanStatus = "✅ Transfer complete! ICS generated.";
                OnReset();
            }
            catch (Exception ex)
            {
                ScanStatus = $"❌ Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OnReset()
        {
            SelectedEquipment = null!;
            SelectedPersonnel = null!;
            ScanStatus = "Scan equipment to begin.";
            LoadPersonnel();
        }
    }
}