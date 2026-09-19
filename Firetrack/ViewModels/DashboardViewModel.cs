using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Firetrack.Converters;
using Firetrack.Helpers;
using Firetrack.Models;
using Firetrack.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using QRCoder;

namespace Firetrack.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private string _fullName = string.Empty;
        private ObservableCollection<EquipmentModel> _myEquipment = new();
        private ObservableCollection<UserModel> _personnelList = new();
        private bool _isAdmin;
        private string _personnelQR = string.Empty;
        private ImageSource? _personnelQRImage;

        private int _totalEquipment;
        private int _availableCount;
        private int _issuedCount;
        private int _damagedCount;
        private int _inRepairCount;
        private int _pendingRequests;
        private int _rejectedRequests;
        private int _disposedCount;

        private ChartDrawable _chartDrawable = new();
        private string _selectedTimeRange = "Last 7 Days";
        private string _chartTitle = "📈 Issued Trend (Last 7 Days)";

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

        public string PersonnelQR
        {
            get => _personnelQR;
            set { _personnelQR = value; OnPropertyChanged(); }
        }

        public ImageSource? PersonnelQRImage
        {
            get => _personnelQRImage;
            set { _personnelQRImage = value; OnPropertyChanged(); }
        }

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
            "Last 7 Days", "Last 30 Days", "Last 90 Days", "Last Year"
        };

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
        public ICommand ReportDamageCommand { get; }
        public ICommand RequestDisposalCommand { get; }
        public ICommand ShowEquipmentDetailsCommand { get; }
        public ICommand DownloadPersonnelQRCommand { get; }

        public DashboardViewModel()
        {
            var user = App.CurrentUser;
            FullName = user?.FullName ?? "Firefighter";
            IsAdmin = user?.Role == "Admin";

            LogoutCommand = new Command(OnLogout);
            DownloadPersonnelQRCommand = new Command(OnDownloadPersonnelQR);

            GoToScannerCommand = new Command(async () =>
            {
                try { await Shell.Current.GoToAsync(Routes.GetScannerPushedRoute()); }
                catch (Exception ex) { await Shell.Current.DisplayAlert("Error", ex.Message, "OK"); }
            });

            GoToTransferCommand = new Command(async () =>
            {
                try { await Shell.Current.GoToAsync(Routes.TransferPushed); }
                catch (Exception ex) { await Shell.Current.DisplayAlert("Error", ex.Message, "OK"); }
            });

            GoToAddUserCommand = new Command(async () =>
            {
                try { await Shell.Current.GoToAsync(Routes.UserManagementPushed); }
                catch (Exception ex) { await Shell.Current.DisplayAlert("Error", ex.Message, "OK"); }
            });

            GoToClearanceCommand = new Command(async () =>
            {
                try { await Shell.Current.GoToAsync(Routes.ClearancePushed); }
                catch (Exception ex) { await Shell.Current.DisplayAlert("Error", ex.Message, "OK"); }
            });

            GoToInventoryCommand = new Command(async () =>
            {
                try { await Shell.Current.GoToAsync(Routes.GetEquipmentCategoryRoute()); }
                catch (Exception ex) { await Shell.Current.DisplayAlert("Error", ex.Message, "OK"); }
            });

            GoToRequestEquipmentCommand = new Command(async () =>
            {
                try { await Shell.Current.GoToAsync(Routes.GetEquipmentCategoryRoute()); }
                catch (Exception ex) { await Shell.Current.DisplayAlert("Error", ex.Message, "OK"); }
            });

            GoToProfileCommand = new Command(async () =>
            {
                try { await Shell.Current.GoToAsync(Routes.ProfilePushed); }
                catch (Exception ex) { await Shell.Current.DisplayAlert("Error", ex.Message, "OK"); }
            });

            GoToUserManagementCommand = new Command(async () =>
            {
                try { await Shell.Current.GoToAsync(Routes.UserManagementPushed); }
                catch (Exception ex) { await Shell.Current.DisplayAlert("Error", ex.Message, "OK"); }
            });

            GoToPendingRequestsCommand = new Command(async () =>
            {
                try { await Shell.Current.GoToAsync(Routes.PendingRequestsPushed); }
                catch (Exception ex) { await Shell.Current.DisplayAlert("Error", ex.Message, "OK"); }
            });

            GoToNotificationsCommand = new Command(async () =>
            {
                try { await Shell.Current.GoToAsync(Routes.Notifications); }
                catch (Exception ex) { await Shell.Current.DisplayAlert("Error", ex.Message, "OK"); }
            });

            ReportDamageCommand = new Command<EquipmentModel>(OnReportDamage);
            RequestDisposalCommand = new Command<EquipmentModel>(OnRequestDisposal);
            ShowEquipmentDetailsCommand = new Command<EquipmentModel>(OnShowEquipmentDetails);

            LoadData();
            LoadMetrics();
            LoadPersonnelQR();
        }

        private async void OnLogout()
        {
            if (App.CurrentUser != null && App.Database != null)
                await App.Database.LogActionAsync(App.CurrentUser.Username, "Logout", "User logged out from Dashboard");

            App.CurrentUser = null;
            if (Shell.Current is AppShell shell) shell.UpdateUserRoleVisibility();
            await Shell.Current.GoToAsync(Routes.Login);
        }

        private void LoadPersonnelQR()
        {
            var user = App.CurrentUser;
            if (user == null || user.Role == "Admin") return;

            PersonnelQR = user.PersonalQR ?? string.Empty;
            if (string.IsNullOrEmpty(PersonnelQR)) return;

            try
            {
                var generator = new QRCodeGenerator();
                var qrCodeData = generator.CreateQrCode(PersonnelQR, QRCodeGenerator.ECCLevel.Q);
                var qrCode = new PngByteQRCode(qrCodeData);
                var pngBytes = qrCode.GetGraphic(20);
                PersonnelQRImage = ImageSource.FromStream(() => new MemoryStream(pngBytes));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ QR error: {ex.Message}");
            }
        }

        private async void OnDownloadPersonnelQR()
        {
            if (string.IsNullOrEmpty(PersonnelQR) || PersonnelQRImage == null)
            {
                await Shell.Current.DisplayAlert("Error", "QR code not available.", "OK");
                return;
            }

            try
            {
                var generator = new QRCodeGenerator();
                var qrCodeData = generator.CreateQrCode(PersonnelQR, QRCodeGenerator.ECCLevel.Q);
                var qrCode = new PngByteQRCode(qrCodeData);
                var pngBytes = qrCode.GetGraphic(20);

                var fileName = $"{PersonnelQR}_{DateTime.Now:yyyyMMddHHmmss}.png";
                var downloadsPath = Path.Combine(FileSystem.AppDataDirectory, "PersonnelQR");
                Directory.CreateDirectory(downloadsPath);
                var filePath = Path.Combine(downloadsPath, fileName);
                await File.WriteAllBytesAsync(filePath, pngBytes);
                await Launcher.Default.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(filePath) });
                await Shell.Current.DisplayAlert("Success", $"QR saved to:\n{filePath}", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void LoadData()
        {
            if (App.CurrentUser == null) return;
            var db = App.Database;
            if (db == null) return;

            if (IsAdmin)
            {
                var users = await db.GetUsersAsync();
                PersonnelList.Clear();
                foreach (var u in users.Where(u => u.Role == "Personnel"))
                    PersonnelList.Add(u);
            }
            else
            {
                var equipment = await db.GetEquipmentsAssignedToUserAsync(App.CurrentUser.Username);
                MyEquipment.Clear();
                foreach (var item in equipment)
                    MyEquipment.Add(item);
            }
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
            LoadPersonnelQR();
        }

        private async void LoadMetrics()
        {
            var db = App.Database;
            if (db == null) return;

            try
            {
                var all = await Task.Run(async () => await db.GetEquipmentsAsync());
                var allTx = await Task.Run(async () => await db.GetTransactionsAsync());

                int total = all.Count;
                int available = all.Count(e => e.Status == "Available");
                int issued = all.Count(e => e.Status == "Issued");
                int damaged = all.Count(e => e.Status == "Damaged");
                int inRepair = all.Count(e => e.Status == "InRepair");
                int pending = all.Count(e => e.RequestStatus == "Pending");
                int rejected = all.Count(e => e.RequestStatus == "Rejected");
                int disposed = all.Count(e => e.Status == "Disposed");

                int days = SelectedTimeRange switch
                {
                    "Last 7 Days" => 7,
                    "Last 30 Days" => 30,
                    "Last 90 Days" => 90,
                    "Last Year" => 365,
                    _ => 7
                };

                var issues = allTx.Where(t => t.Action == "Issue" && t.Timestamp >= DateTime.Now.AddDays(-days));
                var counts = new List<float>();
                for (int i = days - 1; i >= 0; i--)
                {
                    var date = DateTime.Now.Date.AddDays(-i);
                    counts.Add(issues.Count(t => t.Timestamp.Date == date));
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    TotalEquipment = total;
                    AvailableCount = available;
                    IssuedCount = issued;
                    DamagedCount = damaged;
                    InRepairCount = inRepair;
                    PendingRequests = pending;
                    RejectedRequests = rejected;
                    DisposedCount = disposed;

                    ChartDrawable.DataPoints = counts;
                    ChartTitle = $"📈 Issued Trend (Last {days} Days)";
                    OnPropertyChanged(nameof(ChartDrawable));
                    OnPropertyChanged(nameof(ChartTitle));

                    System.Diagnostics.Debug.WriteLine($"📊 Metrics: Total={total}, Avail={available}, Issued={issued}, Damaged={damaged}, Pending={pending}");
                });
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnReportDamage(EquipmentModel? equipment)
        {
            if (equipment == null) return;
            try
            {
                var navParams = new Dictionary<string, object> { { "equipment", equipment } };
                await Shell.Current.GoToAsync(Routes.ReportDamage, navParams);
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnRequestDisposal(EquipmentModel? equipment)
        {
            if (equipment == null) return;
            var db = App.Database;
            if (db == null) return;
            if (App.CurrentUser == null) return;

            if (equipment.Status != "Damaged")
            {
                await Shell.Current.DisplayAlert("Info",
                    "Only damaged equipment can be requested for disposal.", "OK");
                return;
            }

            bool confirm = await Shell.Current.DisplayAlert(
                "Request Disposal",
                $"Request disposal of '{equipment.Name}'?\n\nThis will send a formal request to the Admin for approval.",
                "Yes", "Cancel");
            if (!confirm) return;

            string reason = await Shell.Current.DisplayPromptAsync(
                "Reason for Disposal",
                "Please provide a reason (e.g., damaged beyond repair):",
                "Submit", "Cancel",
                placeholder: "Reason...");

            if (reason == null) return;

            try
            {
                bool success = await db.RequestDisposalAsync(
                    equipment.QRCode,
                    App.CurrentUser.Username,
                    string.IsNullOrWhiteSpace(reason) ? "Damaged equipment" : reason);

                if (success)
                {
                    await Shell.Current.DisplayAlert("Success",
                        $"Disposal request for '{equipment.Name}' submitted to Admin.", "OK");
                    LoadData();
                    LoadMetrics();
                }
                else
                {
                    await Shell.Current.DisplayAlert("Error",
                        "Failed to submit disposal request.", "OK");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnShowEquipmentDetails(EquipmentModel? equipment)
        {
            if (equipment == null) return;
            await Shell.Current.DisplayAlert("Equipment Details",
                $"Name: {equipment.Name}\nQR: {equipment.QRCode}\nType: {equipment.Type}\nStatus: {equipment.Status}\nAssigned to: {equipment.AssignedToUsername ?? "None"}",
                "OK");
        }
    }
}