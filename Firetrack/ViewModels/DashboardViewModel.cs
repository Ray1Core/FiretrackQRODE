using System.Linq;
using Firetrack.Models;
using Firetrack.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Firetrack.Converters;

namespace Firetrack.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        // ---- Existing properties ----
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

        // ---- Metrics properties ----
        private int _totalEquipment;
        private int _availableCount;
        private int _issuedCount;
        private int _damagedCount;
        private int _inRepairCount;
        private int _pendingRequests;
        private int _rejectedRequests;
        private int _disposedCount;
        private ChartDrawable _chartDrawable = new();

        public int TotalEquipment
        {
            get => _totalEquipment;
            set { _totalEquipment = value; OnPropertyChanged(); }
        }

        public int AvailableCount
        {
            get => _availableCount;
            set { _availableCount = value; OnPropertyChanged(); }
        }

        public int IssuedCount
        {
            get => _issuedCount;
            set { _issuedCount = value; OnPropertyChanged(); }
        }

        public int DamagedCount
        {
            get => _damagedCount;
            set { _damagedCount = value; OnPropertyChanged(); }
        }

        public int InRepairCount
        {
            get => _inRepairCount;
            set { _inRepairCount = value; OnPropertyChanged(); }
        }

        public int PendingRequests
        {
            get => _pendingRequests;
            set { _pendingRequests = value; OnPropertyChanged(); }
        }

        public int RejectedRequests
        {
            get => _rejectedRequests;
            set { _rejectedRequests = value; OnPropertyChanged(); }
        }

        public int DisposedCount
        {
            get => _disposedCount;
            set { _disposedCount = value; OnPropertyChanged(); }
        }

        public ChartDrawable ChartDrawable
        {
            get => _chartDrawable;
            set { _chartDrawable = value; OnPropertyChanged(); }
        }

        // ---- Time range picker ----
        private string _selectedTimeRange = "Last 7 Days";
        public string SelectedTimeRange
        {
            get => _selectedTimeRange;
            set
            {
                if (_selectedTimeRange != value)
                {
                    _selectedTimeRange = value;
                    OnPropertyChanged();
                    LoadMetrics();
                }
            }
        }

        public ObservableCollection<string> TimeRangeOptions { get; } = new()
        {
            "Last 7 Days",
            "Last 30 Days",
            "Last 90 Days",
            "Last Year"
        };

        private string _chartTitle = "📈 Issued Trend (Last 7 Days)";
        public string ChartTitle
        {
            get => _chartTitle;
            set { _chartTitle = value; OnPropertyChanged(); }
        }

        // ---- Commands ----
        public ICommand GoToScannerCommand { get; }
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

        // ---- Constructor ----
        public DashboardViewModel()
        {
            var user = App.CurrentUser;
            FullName = user?.FullName ?? "Firefighter";
            IsAdmin = user?.Role == "Admin";

            LogoutCommand = new Command(OnLogout);

            // ✅ All navigation commands use absolute routes (//)
            GoToScannerCommand = new Command(async () =>
            {
                try { await Shell.Current.GoToAsync("//ScannerPage"); }
                catch (Exception ex) { await Shell.Current.DisplayAlert("Error", $"Navigation failed: {ex.Message}", "OK"); }
            });

            GoToTransferCommand = new Command(async () =>
            {
                try { await Shell.Current.GoToAsync("//TransferPage"); }
                catch (Exception ex) { await Shell.Current.DisplayAlert("Error", $"Navigation failed: {ex.Message}", "OK"); }
            });

            GoToAddUserCommand = new Command(async () =>
            {
                try { await Shell.Current.GoToAsync("//UserManagementPage"); }
                catch (Exception ex) { await Shell.Current.DisplayAlert("Error", $"Navigation failed: {ex.Message}", "OK"); }
            });

            GoToClearanceCommand = new Command(async () =>
            {
                try { await Shell.Current.GoToAsync("//ClearancePage"); }
                catch (Exception ex) { await Shell.Current.DisplayAlert("Error", $"Navigation failed: {ex.Message}", "OK"); }
            });

            GoToInventoryCommand = new Command(async () =>
            {
                try { await Shell.Current.GoToAsync("//EquipmentCategoryPage"); }
                catch (Exception ex) { await Shell.Current.DisplayAlert("Error", $"Navigation failed: {ex.Message}", "OK"); }
            });

            GoToRequestEquipmentCommand = new Command(async () =>
            {
                try { await Shell.Current.GoToAsync("//EquipmentCategoryPage"); }
                catch (Exception ex) { await Shell.Current.DisplayAlert("Error", $"Navigation failed: {ex.Message}", "OK"); }
            });

            GoToProfileCommand = new Command(async () =>
            {
                try { await Shell.Current.GoToAsync("//ProfilePage"); }
                catch (Exception ex) { await Shell.Current.DisplayAlert("Error", $"Navigation failed: {ex.Message}", "OK"); }
            });

            GoToUserManagementCommand = new Command(async () =>
            {
                try { await Shell.Current.GoToAsync("//UserManagementPage"); }
                catch (Exception ex) { await Shell.Current.DisplayAlert("Error", $"Navigation failed: {ex.Message}", "OK"); }
            });

            GoToPendingRequestsCommand = new Command(async () =>
            {
                try { await Shell.Current.GoToAsync("//PendingRequestsPage"); }
                catch (Exception ex) { await Shell.Current.DisplayAlert("Error", $"Navigation failed: {ex.Message}", "OK"); }
            });

            GoToNotificationsCommand = new Command(async () =>
            {
                try { await Shell.Current.GoToAsync("//NotificationsPage"); }
                catch (Exception ex) { await Shell.Current.DisplayAlert("Error", $"Navigation failed: {ex.Message}", "OK"); }
            });

            ReturnEquipmentCommand = new Command<EquipmentModel>(OnReturnEquipment);
            ReportDamageCommand = new Command<EquipmentModel>(OnReportDamage);
            ShowEquipmentDetailsCommand = new Command<EquipmentModel>(OnShowEquipmentDetails);

            LoadData();
            LoadMetrics();
        }

        // ---- Logout Method (updated with absolute route) ----
        private async void OnLogout()
        {
            if (App.CurrentUser != null && App.Database != null)
            {
                try
                {
                    await App.Database.LogActionAsync(
                        App.CurrentUser.Username,
                        "Logout",
                        "User logged out from Dashboard");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Logout logging failed: {ex.Message}");
                }
            }

            App.CurrentUser = null;
            if (Shell.Current is AppShell shell)
                shell.UpdateUserRoleVisibility();

            await Shell.Current.GoToAsync("//LoginPage");   // ✅ absolute route
        }

        // ---- Other methods ----
        private async void LoadData()
        {
            if (App.CurrentUser == null) return;
            var db = App.Database;
            if (db == null) return;

            if (IsAdmin)
                await LoadPersonnelList(db);
            else
                await LoadMyEquipment(db);
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
            if (App.CurrentUser == null) return;
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
            LoadMetrics();
        }

        private async void LoadMetrics()
        {
            var db = App.Database;
            if (db == null) return;

            try
            {
                var all = await db.GetEquipmentsAsync();
                TotalEquipment = all.Count;
                AvailableCount = all.Count(e => e.Status == "Available");
                IssuedCount = all.Count(e => e.Status == "Issued");
                DamagedCount = all.Count(e => e.Status == "Damaged");
                InRepairCount = all.Count(e => e.Status == "InRepair");
                PendingRequests = all.Count(e => e.RequestStatus == "Pending");
                RejectedRequests = all.Count(e => e.RequestStatus == "Rejected");
                DisposedCount = all.Count(e => e.Status == "Disposed");

                int days = SelectedTimeRange switch
                {
                    "Last 7 Days" => 7,
                    "Last 30 Days" => 30,
                    "Last 90 Days" => 90,
                    "Last Year" => 365,
                    _ => 7
                };
                ChartTitle = $"📈 Issued Trend (Last {days} Days)";

                var allTx = await db.GetTransactionsAsync();
                var issues = allTx.Where(t => t.Action == "Issue" && t.Timestamp >= DateTime.Now.AddDays(-days));
                var counts = new List<float>();
                for (int i = days - 1; i >= 0; i--)
                {
                    var date = DateTime.Now.Date.AddDays(-i);
                    counts.Add(issues.Count(t => t.Timestamp.Date == date));
                }
                ChartDrawable.DataPoints = counts;
                OnPropertyChanged(nameof(ChartDrawable));
                OnPropertyChanged(nameof(ChartTitle));
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // ---- Return, Report, ShowDetails ----
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
            bool confirm = await Shell.Current.DisplayAlert("Confirm Return", $"Return '{equipment.Name}'?", "Yes", "Cancel");
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

                if (App.CurrentUser != null)
                    await db.LogActionAsync(App.CurrentUser.Username, "Return Equipment", $"Returned '{equipment.Name}' ({equipment.QRCode})");

                await db.SendNotificationAsync("admin", "↩️ Equipment Returned", $"{App.CurrentUser?.FullName} returned '{equipment.Name}'.");
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
            try
            {
                var navigationParams = new Dictionary<string, object> { { "equipment", equipment } };
                await Shell.Current.GoToAsync("ReportDamagePage", navigationParams);
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Navigation failed: {ex.Message}", "OK");
            }
        }

        private async void OnShowEquipmentDetails(EquipmentModel? equipment)
        {
            if (equipment == null) return;
            await Shell.Current.DisplayAlert(
                "Equipment Details",
                $"Name: {equipment.Name}\nQR: {equipment.QRCode}\nType: {equipment.Type}\nStatus: {equipment.Status}\nAssigned to: {equipment.AssignedToUsername ?? "None"}\nRemarks: {equipment.Remarks ?? "None"}",
                "OK");
        }
    }
}