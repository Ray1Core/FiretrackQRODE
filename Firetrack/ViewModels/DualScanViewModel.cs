// NEW: DualScanViewModel.cs
using Firetrack.Models;
using Firetrack.Services;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class DualScanViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private bool _isAdminScanComplete;
        private bool _isPersonnelScanComplete;
        private string _adminUsername = string.Empty;
        private string _personnelUsername = string.Empty;
        private EquipmentModel _selectedEquipment = null!;
        private string _scanStatus = "Waiting for Admin scan...";
        private bool _isBusy;

        public bool IsAdminScanComplete
        {
            get => _isAdminScanComplete;
            set { _isAdminScanComplete = value; OnPropertyChanged(); }
        }

        public bool IsPersonnelScanComplete
        {
            get => _isPersonnelScanComplete;
            set { _isPersonnelScanComplete = value; OnPropertyChanged(); }
        }

        public string AdminUsername
        {
            get => _adminUsername;
            set { _adminUsername = value; OnPropertyChanged(); }
        }

        public string PersonnelUsername
        {
            get => _personnelUsername;
            set { _personnelUsername = value; OnPropertyChanged(); }
        }

        public EquipmentModel SelectedEquipment
        {
            get => _selectedEquipment;
            set { _selectedEquipment = value; OnPropertyChanged(); }
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

        public ICommand ScanAdminCommand { get; }
        public ICommand ScanPersonnelCommand { get; }
        public ICommand ScanEquipmentCommand { get; }
        public ICommand ConfirmTransferCommand { get; }
        public ICommand ResetCommand { get; }

        public DualScanViewModel()
        {
            _db = App.Database!;
            ScanAdminCommand = new Command(OnScanAdmin);
            ScanPersonnelCommand = new Command(OnScanPersonnel);
            ScanEquipmentCommand = new Command(OnScanEquipment);
            ConfirmTransferCommand = new Command(OnConfirmTransfer);
            ResetCommand = new Command(OnReset);
        }

        private async void OnScanAdmin()
        {
            // Navigate to scanner for Admin QR
            var result = await Shell.Current.DisplayPromptAsync(
                "Admin Verification",
                "Scan Admin QR Code:",
                "Scan",
                "Cancel");

            if (!string.IsNullOrEmpty(result))
            {
                var user = await _db.GetUserByUsernameAsync(result);
                if (user != null && user.Role == "Admin")
                {
                    AdminUsername = user.Username;
                    IsAdminScanComplete = true;
                    ScanStatus = "✅ Admin verified. Scan Personnel now.";
                }
                else
                {
                    ScanStatus = "❌ Invalid Admin QR. Try again.";
                }
            }
        }

        private async void OnScanPersonnel()
        {
            if (!IsAdminScanComplete)
            {
                ScanStatus = "⚠️ Scan Admin first!";
                return;
            }

            var result = await Shell.Current.DisplayPromptAsync(
                "Personnel Verification",
                "Scan Personnel QR Code:",
                "Scan",
                "Cancel");

            if (!string.IsNullOrEmpty(result))
            {
                var user = await _db.GetUserByUsernameAsync(result);
                if (user != null && user.Role == "Personnel")
                {
                    PersonnelUsername = user.Username;
                    IsPersonnelScanComplete = true;
                    ScanStatus = "✅ Personnel verified. Scan equipment now.";
                }
                else
                {
                    ScanStatus = "❌ Invalid Personnel QR. Try again.";
                }
            }
        }

        private async void OnScanEquipment()
        {
            if (!IsAdminScanComplete || !IsPersonnelScanComplete)
            {
                ScanStatus = "⚠️ Verify both Admin and Personnel first!";
                return;
            }

            var result = await Shell.Current.DisplayPromptAsync(
                "Equipment Scan",
                "Scan Equipment QR Code:",
                "Scan",
                "Cancel");

            if (!string.IsNullOrEmpty(result))
            {
                var equipment = await _db.GetEquipmentByQRAsync(result);
                if (equipment != null && equipment.Status == "Available")
                {
                    SelectedEquipment = equipment;
                    ScanStatus = $"✅ Equipment '{equipment.Name}' ready for transfer.";
                }
                else
                {
                    ScanStatus = "❌ Equipment not available or invalid QR.";
                }
            }
        }

        private async void OnConfirmTransfer()
        {
            if (SelectedEquipment == null)
            {
                ScanStatus = "⚠️ Scan equipment first!";
                return;
            }

            bool confirm = await Shell.Current.DisplayAlert(
                "Confirm Transfer",
                $"Transfer '{SelectedEquipment.Name}' from {AdminUsername} to {PersonnelUsername}?",
                "Yes",
                "Cancel");

            if (!confirm) return;

            IsBusy = true;
            try
            {
                // Get the actual user objects
                var admin = await _db.GetUserByUsernameAsync(AdminUsername);
                var personnel = await _db.GetUserByUsernameAsync(PersonnelUsername);

                if (admin == null || personnel == null)
                {
                    ScanStatus = "❌ User not found.";
                    IsBusy = false;
                    return;
                }

                // Record the transaction
                var transaction = new TransactionModel
                {
                    EquipmentQR = SelectedEquipment.QRCode,
                    FromUser = admin.Username,
                    ToUser = personnel.Username,
                    Timestamp = DateTime.Now,
                    Action = "Issue",
                    Remarks = $"Dual-scan handshake completed by {admin.FullName} → {personnel.FullName}"
                };

                // Update equipment
                SelectedEquipment.AssignedToUsername = personnel.Username;
                SelectedEquipment.Status = "Issued";
                SelectedEquipment.LastUpdated = DateTime.Now;

                await _db.SaveTransactionAsync(transaction);
                await _db.SaveEquipmentAsync(SelectedEquipment);

                // Log the dual-scan event
                await _db.LogActionAsync(
                    admin.Username,
                    "Dual-Scan Transfer",
                    $"Issued '{SelectedEquipment.Name}' to {personnel.Username} via dual-scan");

                // Notify both parties
                await _db.SendNotificationAsync(
                    personnel.Username,
                    "🔄 Equipment Issued",
                    $"{admin.FullName} issued '{SelectedEquipment.Name}' to you via dual-scan."
                );

                await _db.SendNotificationAsync(
                    admin.Username,
                    "✅ Transfer Complete",
                    $"You issued '{SelectedEquipment.Name}' to {personnel.FullName} via dual-scan."
                );

                // Navigate to ICS page
                var navParams = new Dictionary<string, object>
                {
                    { "equipment", SelectedEquipment },
                    { "officer", personnel }
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
            IsAdminScanComplete = false;
            IsPersonnelScanComplete = false;
            AdminUsername = string.Empty;
            PersonnelUsername = string.Empty;
            SelectedEquipment = null!;
            ScanStatus = "Waiting for Admin scan...";
        }
    }
}