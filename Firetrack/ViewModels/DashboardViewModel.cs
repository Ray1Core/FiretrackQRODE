using System.Linq;
using Firetrack.Models;
using Firetrack.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private string _fullName = string.Empty;
        private ObservableCollection<EquipmentModel> _myEquipment = new();
        private ObservableCollection<UserModel> _personnelList = new();
        private bool _isAdmin;

        public string FullName
        {
            get => _fullName;
            set { _fullName = value; OnPropertyChanged(); }
        }

        public string UserRole => App.CurrentUser?.Role ?? "Guest";

        public ObservableCollection<EquipmentModel> MyEquipment
        {
            get => _myEquipment;
            set { _myEquipment = value; OnPropertyChanged(); }
        }

        public ObservableCollection<UserModel> PersonnelList
        {
            get => _personnelList;
            set { _personnelList = value; OnPropertyChanged(); }
        }

        public bool IsAdmin
        {
            get => _isAdmin;
            set { _isAdmin = value; OnPropertyChanged(); }
        }

        public ICommand ToggleFlyoutCommand { get; }
        public ICommand GoToScannerCommand { get; }
        public ICommand GoToGenerateCommand { get; }
        public ICommand GoToTransferCommand { get; }
        public ICommand GoToAddUserCommand { get; }
        public ICommand GoToClearanceCommand { get; }
        public ICommand GoToInventoryCommand { get; }
        public ICommand GoToRequestEquipmentCommand { get; }
        public ICommand GoToProfileCommand { get; }
        public ICommand GoToUserManagementCommand { get; }
        public ICommand GoToPendingRequestsCommand { get; }
        public ICommand GoToNotificationsCommand { get; }
        public ICommand LogoutCommand { get; }

        public ICommand ReturnEquipmentCommand { get; }
        public ICommand ReportDamageCommand { get; }
        public ICommand ShowEquipmentDetailsCommand { get; }

        public DashboardViewModel()
        {
            var user = App.CurrentUser;
            FullName = user?.FullName ?? "Firefighter";
            IsAdmin = user?.Role == "Admin";

            ToggleFlyoutCommand = new Command(ToggleFlyout);
            LogoutCommand = new Command(OnLogout);

            // Navigation commands (absolute routes for root pages, relative for others)
            GoToScannerCommand = new Command(async () => await Shell.Current.GoToAsync("ScannerPage"));
            GoToGenerateCommand = new Command(async () => await Shell.Current.GoToAsync("GenerateQRPage"));
            GoToTransferCommand = new Command(async () => await Shell.Current.GoToAsync("TransferPage"));
            GoToAddUserCommand = new Command(async () => await Shell.Current.GoToAsync("AddUserPage"));
            GoToClearanceCommand = new Command(async () => await Shell.Current.GoToAsync("ClearancePage"));
            GoToInventoryCommand = new Command(async () => await Shell.Current.GoToAsync("InventoryPage"));
            GoToRequestEquipmentCommand = new Command(async () => await Shell.Current.GoToAsync("RequestEquipmentPage"));
            GoToProfileCommand = new Command(async () => await Shell.Current.GoToAsync("ProfilePage"));
            GoToUserManagementCommand = new Command(async () => await Shell.Current.GoToAsync("UserManagementPage"));
            GoToPendingRequestsCommand = new Command(async () => await Shell.Current.GoToAsync("PendingRequestsPage"));
            GoToNotificationsCommand = new Command(async () => await Shell.Current.GoToAsync("NotificationsPage"));

            ReturnEquipmentCommand = new Command<EquipmentModel>(OnReturnEquipment);
            ReportDamageCommand = new Command<EquipmentModel>(OnReportDamage);
            ShowEquipmentDetailsCommand = new Command<EquipmentModel>(OnShowEquipmentDetails);

            LoadData();
        }

        private void ToggleFlyout() => Shell.Current.FlyoutIsPresented = !Shell.Current.FlyoutIsPresented;

        private async void OnLogout()
        {
            App.CurrentUser = null;
            if (Shell.Current is AppShell shell) shell.UpdateUserRoleVisibility();
            await Shell.Current.GoToAsync("//LoginPage");
        }

        private async void LoadData()
        {
            if (App.CurrentUser == null)
                return;

            var db = App.Database;
            if (db == null)
                return;

            if (IsAdmin)
            {
                await LoadPersonnelList(db);
            }
            else
            {
                await LoadMyEquipment(db);
            }
        }

        private async Task LoadPersonnelList(DatabaseService db)
        {
            var users = await db.GetUsersAsync();
            PersonnelList.Clear();
            foreach (var u in users.Where(u => u.Role == "Personnel"))
                PersonnelList.Add(u);
        }

        private async Task LoadMyEquipment(DatabaseService db)
        {
            var equipment = await db.GetEquipmentsAssignedToUserAsync(App.CurrentUser.Username);
            MyEquipment.Clear();
            foreach (var item in equipment)
                MyEquipment.Add(item);
        }

        public void RefreshDashboard()
        {
            var user = App.CurrentUser;
            FullName = user?.FullName ?? "Firefighter";
            IsAdmin = user?.Role == "Admin";
            OnPropertyChanged(nameof(FullName));
            OnPropertyChanged(nameof(IsAdmin));
            OnPropertyChanged(nameof(UserRole));
            LoadData();
        }

        private async void OnReturnEquipment(EquipmentModel? equipment)
        {
            if (equipment == null) return;

            var db = App.Database;
            if (db == null)
            {
                await Shell.Current.DisplayAlert("Error", "Database not available.", "OK");
                return;
            }

            if (App.CurrentUser == null || equipment.AssignedToUsername != App.CurrentUser.Username)
            {
                await Shell.Current.DisplayAlert("Error", "This equipment is not assigned to you.", "OK");
                return;
            }

            bool confirm = await Shell.Current.DisplayAlert(
                "Confirm Return",
                $"Return '{equipment.Name}'?",
                "Yes",
                "Cancel");

            if (!confirm) return;

            try
            {
                equipment.Status = "Available";
                equipment.AssignedToUsername = null;
                equipment.LastUpdated = DateTime.Now;

                var transaction = new TransactionModel
                {
                    EquipmentQR = equipment.QRCode,
                    FromUser = App.CurrentUser.Username,
                    ToUser = "System",
                    Timestamp = DateTime.Now,
                    Action = "Return",
                    Remarks = $"Returned by {App.CurrentUser.FullName}"
                };

                await db.SaveEquipmentAsync(equipment);
                await db.SaveTransactionAsync(transaction);

                // ✅ Log the action
                if (App.CurrentUser != null)
                {
                    await db.LogActionAsync(
                        App.CurrentUser.Username,
                        "Return Equipment",
                        $"Returned '{equipment.Name}' ({equipment.QRCode})");
                }

                await db.SendNotificationAsync(
                    "admin",
                    "↩️ Equipment Returned",
                    $"{App.CurrentUser?.FullName} returned '{equipment.Name}'.");

                await Shell.Current.DisplayAlert("Success", $"'{equipment.Name}' returned successfully.", "OK");
                LoadData();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnReportDamage(EquipmentModel equipment)
        {
            if (equipment == null) return;
            var navigationParams = new Dictionary<string, object> { { "equipment", equipment } };
            await Shell.Current.GoToAsync("ReportDamagePage", navigationParams);
        }

        private async void OnShowEquipmentDetails(EquipmentModel? equipment)
        {
            if (equipment == null) return;

            await Shell.Current.DisplayAlert(
                "Equipment Details",
                $"Name: {equipment.Name}\n" +
                $"QR: {equipment.QRCode}\n" +
                $"Type: {equipment.Type}\n" +
                $"Status: {equipment.Status}\n" +
                $"Assigned to: {equipment.AssignedToUsername ?? "None"}\n" +
                $"Remarks: {equipment.Remarks ?? "None"}",
                "OK");
        }
    }
}