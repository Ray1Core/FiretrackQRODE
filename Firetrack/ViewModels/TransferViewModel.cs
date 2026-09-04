using Firetrack.Models;
using Firetrack.Services;
using Firetrack.Helpers;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Firetrack.ViewModels
{
    public class TransferViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;

        private EquipmentModel _selectedEquipment = null!;
        private UserModel _currentCustodian = null!;
        private UserModel _selectedPersonnel = null!;

        private string _step1Status = "Scan equipment to begin.";
        private string _step2Status = "Scan or select the current custodian.";
        private string _step3Status = "Scan or select the receiving personnel.";
        private string _step4Status = "Waiting for both confirmations.";

        private bool _fromConfirmed;
        private bool _toConfirmed;
        private bool _isBusy;

        private ObservableCollection<UserModel> _personnelList = new();

        // -----------------------------
        // EQUIPMENT
        // -----------------------------

        public EquipmentModel SelectedEquipment
        {
            get => _selectedEquipment;
            set
            {
                _selectedEquipment = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsStep1Complete));
                OnPropertyChanged(nameof(CanConfirmTransfer));
            }
        }

        // -----------------------------
        // CURRENT CUSTODIAN
        // -----------------------------

        public UserModel CurrentCustodian
        {
            get => _currentCustodian;
            set
            {
                _currentCustodian = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsStep2Complete));

                Step2Status = value != null
                    ? $"✅ Current Custodian: {value.FullName}"
                    : "Scan or select the current custodian.";

                OnPropertyChanged(nameof(CanConfirmTransfer));
            }
        }

        // -----------------------------
        // RECEIVING PERSONNEL
        // -----------------------------

        public UserModel SelectedPersonnel
        {
            get => _selectedPersonnel;
            set
            {
                _selectedPersonnel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsStep3Complete));

                Step3Status = value != null
                    ? $"✅ Receiving Personnel: {value.FullName}"
                    : "Scan or select the receiving personnel.";

                OnPropertyChanged(nameof(CanConfirmTransfer));
            }
        }

        public ObservableCollection<UserModel> PersonnelList
        {
            get => _personnelList;
            set
            {
                _personnelList = value;
                OnPropertyChanged();
            }
        }

        // -----------------------------
        // STATUS TEXT
        // -----------------------------

        public string Step1Status
        {
            get => _step1Status;
            set
            {
                _step1Status = value;
                OnPropertyChanged();
            }
        }

        public string Step2Status
        {
            get => _step2Status;
            set
            {
                _step2Status = value;
                OnPropertyChanged();
            }
        }

        public string Step3Status
        {
            get => _step3Status;
            set
            {
                _step3Status = value;
                OnPropertyChanged();
            }
        }

        public string Step4Status
        {
            get => _step4Status;
            set
            {
                _step4Status = value;
                OnPropertyChanged();
            }
        }

        // -----------------------------
        // CONFIRMATION
        // -----------------------------

        public bool FromConfirmed
        {
            get => _fromConfirmed;
            set
            {
                _fromConfirmed = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanConfirmTransfer));
            }
        }

        public bool ToConfirmed
        {
            get => _toConfirmed;
            set
            {
                _toConfirmed = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanConfirmTransfer));
            }
        }

        // -----------------------------
        // BUSY
        // -----------------------------

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
            }
        }

        // -----------------------------
        // STEP COMPLETION
        // -----------------------------

        public bool IsStep1Complete => SelectedEquipment != null;
        public bool IsStep2Complete => CurrentCustodian != null;
        public bool IsStep3Complete => SelectedPersonnel != null;

        public bool CanConfirmTransfer =>
            SelectedEquipment != null &&
            CurrentCustodian != null &&
            SelectedPersonnel != null &&
            FromConfirmed &&
            ToConfirmed;

        // -----------------------------
        // COMMANDS
        // -----------------------------

        public ICommand ScanEquipmentCommand { get; }
        public ICommand ScanCurrentCustodianCommand { get; }
        public ICommand ScanNewCustodianCommand { get; }

        public ICommand ConfirmFromCommand { get; }
        public ICommand ConfirmToCommand { get; }

        public ICommand CompleteTransferCommand { get; }
        public ICommand ResetCommand { get; }

        public TransferViewModel()
        {
            _db = App.Database!;

            // ---- UPDATED: all scan commands now use Routes.GetScannerRoute() ----
            ScanEquipmentCommand = new Command(OnScanEquipment);
            ScanCurrentCustodianCommand = new Command(OnScanCurrentCustodian);
            ScanNewCustodianCommand = new Command(OnScanNewCustodian);

            ConfirmFromCommand = new Command(OnConfirmFrom);
            ConfirmToCommand = new Command(OnConfirmTo);

            CompleteTransferCommand = new Command(OnCompleteTransfer);
            ResetCommand = new Command(OnReset);

            LoadPersonnel();
        }

        // =========================================================
        // STEP 1 - SCAN EQUIPMENT
        // =========================================================

        private async void OnScanEquipment()
        {
            var parameters = new Dictionary<string, object>
            {
                { "returnTo", "TransferPage" },
                { "mode", "equipment" }
            };
            // Use role-aware scanner route
            await Shell.Current.GoToAsync(Routes.GetScannerRoute(), parameters);
        }

        // =========================================================
        // STEP 2 - SCAN CURRENT CUSTODIAN
        // =========================================================

        private async void OnScanCurrentCustodian()
        {
            var parameters = new Dictionary<string, object>
            {
                { "returnTo", "TransferPage" },
                { "mode", "currentCustodian" }
            };
            // Use role-aware scanner route
            await Shell.Current.GoToAsync(Routes.GetScannerRoute(), parameters);
        }

        // =========================================================
        // STEP 3 - SCAN RECEIVING PERSON
        // =========================================================

        private async void OnScanNewCustodian()
        {
            var parameters = new Dictionary<string, object>
            {
                { "returnTo", "TransferPage" },
                { "mode", "newCustodian" }
            };
            // Use role-aware scanner route
            await Shell.Current.GoToAsync(Routes.GetScannerRoute(), parameters);
        }

        // =========================================================
        // PROCESS QR
        // =========================================================

        public async Task ProcessScannedQR(string qrCode, string mode)
        {
            if (string.IsNullOrWhiteSpace(qrCode))
                return;

            IsBusy = true;

            try
            {
                // -----------------------------
                // EQUIPMENT QR
                // -----------------------------

                if (mode == "equipment")
                {
                    var equipment = await _db.GetEquipmentByQRAsync(qrCode);

                    if (equipment == null)
                    {
                        Step1Status = "❌ Equipment not found or invalid QR.";
                        return;
                    }

                    SelectedEquipment = equipment;
                    Step1Status = $"✅ Equipment scanned: {equipment.Name}";

                    // Automatically identify current custodian
                    if (!string.IsNullOrWhiteSpace(equipment.AssignedToUsername))
                    {
                        var users = await _db.GetUsersAsync();
                        var custodian = users.FirstOrDefault(
                            u => u.Username == equipment.AssignedToUsername);

                        if (custodian != null)
                        {
                            CurrentCustodian = custodian;
                        }
                    }
                }

                // -----------------------------
                // CURRENT CUSTODIAN QR
                // -----------------------------

                else if (mode == "currentCustodian")
                {
                    var user = await _db.GetUserByUsernameAsync(qrCode);

                    if (user == null)
                    {
                        Step2Status = "❌ User QR not found.";
                        return;
                    }

                    CurrentCustodian = user;
                }

                // -----------------------------
                // NEW CUSTODIAN QR
                // -----------------------------

                else if (mode == "newCustodian")
                {
                    var user = await _db.GetUserByUsernameAsync(qrCode);

                    if (user == null)
                    {
                        Step3Status = "❌ User QR not found.";
                        return;
                    }

                    if (!user.IsActive)
                    {
                        Step3Status = "❌ This user account is inactive.";
                        return;
                    }

                    if (CurrentCustodian != null &&
                        user.Username == CurrentCustodian.Username)
                    {
                        Step3Status = "❌ Receiving user cannot be the current custodian.";
                        return;
                    }

                    SelectedPersonnel = user;
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

        // =========================================================
        // STEP 4 - CURRENT CUSTODIAN CONFIRMATION
        // =========================================================

        private async void OnConfirmFrom()
        {
            if (CurrentCustodian == null)
                return;

            FromConfirmed = true;
            Step4Status = $"✓ {CurrentCustodian.FullName} confirmed the handover.";
            await Task.CompletedTask;
        }

        // =========================================================
        // STEP 4 - RECEIVING PERSON CONFIRMATION
        // =========================================================

        private async void OnConfirmTo()
        {
            if (SelectedPersonnel == null)
                return;

            ToConfirmed = true;
            Step4Status = $"✓ {SelectedPersonnel.FullName} confirmed receiving the equipment.";
            await Task.CompletedTask;
        }

        // =========================================================
        // COMPLETE TRANSFER
        // =========================================================

        private async void OnCompleteTransfer()
        {
            if (!CanConfirmTransfer)
                return;

            bool confirm = await Shell.Current.DisplayAlert(
                "Confirm Transfer",
                $"Transfer '{SelectedEquipment.Name}' " +
                $"from {CurrentCustodian.FullName} " +
                $"to {SelectedPersonnel.FullName}?",
                "Confirm",
                "Cancel");

            if (!confirm)
                return;

            IsBusy = true;

            try
            {
                var transaction = new TransactionModel
                {
                    EquipmentQR = SelectedEquipment.QRCode,
                    FromUser = CurrentCustodian.Username,
                    ToUser = SelectedPersonnel.Username,
                    Timestamp = DateTime.Now,
                    Action = "Transfer",
                    Remarks = $"Digital Handshake: " +
                              $"{CurrentCustodian.FullName} " +
                              $"transferred '{SelectedEquipment.Name}' " +
                              $"to {SelectedPersonnel.FullName}."
                };

                SelectedEquipment.AssignedToUsername = SelectedPersonnel.Username;
                SelectedEquipment.Status = "Issued";
                SelectedEquipment.LastUpdated = DateTime.Now;

                await _db.SaveTransactionAsync(transaction);
                await _db.SaveEquipmentAsync(SelectedEquipment);

                await _db.LogActionAsync(
                    CurrentCustodian.Username,
                    "Digital Handshake",
                    $"Transferred '{SelectedEquipment.Name}' " +
                    $"to {SelectedPersonnel.FullName}");

                await _db.SendNotificationAsync(
                    SelectedPersonnel.Username,
                    "🤝 Equipment Transfer Complete",
                    $"{CurrentCustodian.FullName} " +
                    $"transferred '{SelectedEquipment.Name}' " +
                    $"to you.");

                Step4Status = "✅ Transfer completed successfully.";

                await Shell.Current.DisplayAlert(
                    "Transfer Complete",
                    $"'{SelectedEquipment.Name}' " +
                    $"is now assigned to " +
                    $"{SelectedPersonnel.FullName}.",
                    "OK");

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

        // =========================================================
        // LOAD PERSONNEL
        // =========================================================

        private async void LoadPersonnel()
        {
            var users = await _db.GetUsersAsync();

            PersonnelList.Clear();

            foreach (var user in users.Where(u => u.Role == "Personnel" && u.IsActive))
            {
                PersonnelList.Add(user);
            }
        }

        // =========================================================
        // RESET
        // =========================================================

        private void OnReset()
        {
            SelectedEquipment = null!;
            CurrentCustodian = null!;
            SelectedPersonnel = null!;

            FromConfirmed = false;
            ToConfirmed = false;

            Step1Status = "Scan equipment to begin.";
            Step2Status = "Scan or select the current custodian.";
            Step3Status = "Scan or select the receiving personnel.";
            Step4Status = "Waiting for both confirmations.";

            LoadPersonnel();
        }
    }
}