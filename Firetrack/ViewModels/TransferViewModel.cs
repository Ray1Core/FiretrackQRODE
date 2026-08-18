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
        private string _step1Status = "Scan equipment to begin.";
        private string _step3Status = "Select a personnel.";
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
            set { _selectedPersonnel = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanTransfer)); Step3Status = value != null ? "✅ Personnel selected." : "Select a personnel."; }
        }

        public ObservableCollection<UserModel> PersonnelList
        {
            get => _personnelList;
            set { _personnelList = value; OnPropertyChanged(); }
        }

        public string Step1Status
        {
            get => _step1Status;
            set { _step1Status = value; OnPropertyChanged(); }
        }

        public string Step3Status
        {
            get => _step3Status;
            set { _step3Status = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public bool IsStep1Complete => SelectedEquipment != null;
        public bool CanTransfer => SelectedEquipment != null && SelectedPersonnel != null && SelectedEquipment.Status == "Available";

        public string CurrentUserFullName => App.CurrentUser?.FullName ?? "Unknown";

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

        // ---- Scan Equipment: Navigate to Scanner with mode parameter ----
        private async void OnScanEquipment()
        {
            // ✅ Pass mode=equipment to scanner
            await Shell.Current.GoToAsync($"//ScannerPage?returnTo=TransferPage&mode=equipment");
        }

        // ---- Process scanned QR with mode ----
        public async Task ProcessScannedQR(string qrCode, string mode)
        {
            if (string.IsNullOrWhiteSpace(qrCode))
                return;

            IsBusy = true;

            try
            {
                if (mode == "equipment")
                {
                    var equipment = await _db.GetEquipmentByQRAsync(qrCode);
                    if (equipment != null && equipment.Status == "Available")
                    {
                        SelectedEquipment = equipment;
                        Step1Status = $"✅ Equipment '{equipment.Name}' ready for transfer.";
                    }
                    else if (equipment != null && equipment.Status != "Available")
                    {
                        Step1Status = $"❌ Equipment '{equipment.Name}' is not available (Status: {equipment.Status}).";
                    }
                    else
                    {
                        Step1Status = "❌ Equipment not found or invalid QR.";
                    }
                }
                // Add other modes if needed (e.g., "personnel", "admin")
                else
                {
                    Step1Status = $"❌ Unknown scan mode: {mode}";
                }
            }
            catch (Exception ex)
            {
                Step1Status = $"❌ Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ---- Confirm Transfer ----
        private async void OnConfirmTransfer()
        {
            if (!CanTransfer) return;

            bool confirm = await Shell.Current.DisplayAlert(
                "Confirm Transfer",
                $"Transfer '{SelectedEquipment.Name}' to {SelectedPersonnel.FullName}?",
                "Yes", "Cancel");

            if (!confirm) return;

            IsBusy = true;
            try
            {
                var admin = App.CurrentUser;
                if (admin == null)
                {
                    await Shell.Current.DisplayAlert("Error", "Admin not logged in.", "OK");
                    IsBusy = false;
                    return;
                }

                // Save transaction
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

                await _db.LogActionAsync(admin.Username, "Transfer", $"Issued '{SelectedEquipment.Name}' to {SelectedPersonnel.Username}");
                await _db.SendNotificationAsync(SelectedPersonnel.Username, "🔄 Equipment Issued", $"{admin.FullName} issued '{SelectedEquipment.Name}' to you.");

                // Navigate to ICS
                var navParams = new Dictionary<string, object>
                {
                    { "equipment", SelectedEquipment },
                    { "officer", SelectedPersonnel }
                };
                await Shell.Current.GoToAsync("//IcsPage", navParams);

                OnReset();
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

        private void OnReset()
        {
            SelectedEquipment = null!;
            SelectedPersonnel = null!;
            Step1Status = "Scan equipment to begin.";
            Step3Status = "Select a personnel.";
            LoadPersonnel();
        }
    }
}