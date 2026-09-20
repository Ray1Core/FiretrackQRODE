using Firetrack.Models;
using Firetrack.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.IO;
using System.Linq;

namespace Firetrack.ViewModels
{
    public class ClearanceViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private ObservableCollection<UserModel> _officers = new();
        private UserModel? _selectedOfficer;
        private ObservableCollection<EquipmentModel> _assignedEquipment = new();
        private EquipmentModel? _selectedEquipment;
        private string _statusMessage = string.Empty;
        private bool _isBusy;

        public ObservableCollection<UserModel> Officers
        {
            get => _officers;
            set { _officers = value; OnPropertyChanged(); }
        }

        public UserModel? SelectedOfficer
        {
            get => _selectedOfficer;
            set
            {
                _selectedOfficer = value;
                OnPropertyChanged();
                LoadAssignedEquipment();
            }
        }

        public ObservableCollection<EquipmentModel> AssignedEquipment
        {
            get => _assignedEquipment;
            set { _assignedEquipment = value; OnPropertyChanged(); }
        }

        public EquipmentModel? SelectedEquipment
        {
            get => _selectedEquipment;
            set { _selectedEquipment = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand MarkReturnedCommand { get; }
        public ICommand MarkAllReturnedCommand { get; }
        public ICommand GenerateCertificateCommand { get; }
        public ICommand RefreshCommand { get; }

        public ClearanceViewModel()
        {
            _db = App.Database!;
            MarkReturnedCommand = new Command(OnMarkReturned);
            MarkAllReturnedCommand = new Command(OnMarkAllReturned);
            GenerateCertificateCommand = new Command(OnGenerateCertificate);
            RefreshCommand = new Command(OnRefresh);
            LoadOfficers();
        }

        // ============================================================
        // PUBLIC REFRESH — safe to call from Page.OnAppearing
        // ============================================================
        public void RefreshAsync()
        {
            LoadOfficers();
            if (SelectedOfficer != null)
                LoadAssignedEquipment();
        }

        private async void LoadOfficers()
        {
            var users = await _db.GetUsersAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Officers.Clear();
                foreach (var u in users.Where(u => u.Role == "Personnel"))
                    Officers.Add(u);
            });
        }

        private async void LoadAssignedEquipment()
        {
            if (SelectedOfficer == null)
            {
                AssignedEquipment.Clear();
                return;
            }

            IsBusy = true;

            try
            {
                var officer = SelectedOfficer;   // ✅ local for null-safety
                var equipment = await _db.GetEquipmentsAssignedToUserAsync(officer.Username);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    AssignedEquipment.Clear();
                    foreach (var eq in equipment)
                        AssignedEquipment.Add(eq);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ LoadAssignedEquipment failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ============================================================
        // MARK SELECTED AS RETURNED
        // ------------------------------------------------------------
        // Closes the Assignments row FIRST, then updates the Equipment
        // row. Damaged/InRepair items keep their status so they stay
        // in the disposal pipeline.
        //
        // NEW: notify the officer so their dashboard badge updates.
        // ============================================================
        private async void OnMarkReturned()
        {
            if (SelectedEquipment == null)
            {
                StatusMessage = "Please select an equipment to mark as returned.";
                return;
            }

            if (SelectedEquipment.Status == "Available" &&
                string.IsNullOrEmpty(SelectedEquipment.AssignedToUsername))
            {
                StatusMessage = "This equipment is already marked as returned.";
                return;
            }

            IsBusy = true;
            StatusMessage = string.Empty;

            try
            {
                // ✅ FIX: close the Assignments row FIRST so the officer's
                // dashboard JOIN no longer finds this equipment.
                if (SelectedOfficer != null)
                {
                    await _db.CloseActiveAssignmentsAsync(
                        SelectedEquipment.EquipmentId,
                        SelectedOfficer.Username);
                }

                var transaction = new TransactionModel
                {
                    EquipmentQR = SelectedEquipment.QRCode,
                    FromUser = SelectedEquipment.AssignedToUsername ?? "unknown",
                    ToUser = App.CurrentUser?.Username ?? "admin",
                    Timestamp = DateTime.Now,
                    Action = "Return",
                    Remarks = $"Returned by {SelectedEquipment.AssignedToUsername} during clearance."
                };

                // Don't downgrade Damaged/InRepair items to Available
                string originalStatus = SelectedEquipment.Status;
                string? itemName = SelectedEquipment.Name;
                string officerUsername = SelectedOfficer?.Username ?? "unknown";
                string officerFullName = SelectedOfficer?.FullName ?? "unknown";

                SelectedEquipment.AssignedToUsername = null;

                if (originalStatus == "Damaged" || originalStatus == "InRepair")
                {
                    StatusMessage = $"⚠️ '{itemName}' is {originalStatus}. " +
                                    "Assignment cleared; please process disposal separately.";
                }
                else
                {
                    SelectedEquipment.Status = "Available";
                    StatusMessage = $"✅ {itemName} marked as returned.";
                }

                SelectedEquipment.LastUpdated = DateTime.Now;

                await _db.SaveTransactionAsync(transaction);
                await _db.SaveEquipmentAsync(SelectedEquipment);

                if (App.CurrentUser != null)
                {
                    await _db.LogActionAsync(
                        App.CurrentUser.Username,
                        "Clearance Return",
                        $"Marked '{itemName}' as returned (previous status: {originalStatus}).");
                }

                // ✅ NEW: notify the officer (best-effort)
                try
                {
                    await _db.SendNotificationAsync(
                        officerUsername,
                        "✅ Equipment Cleared",
                        $"'{itemName}' has been cleared from your accountability by " +
                        $"{App.CurrentUser?.FullName ?? "Admin"}.");
                }
                catch (Exception nEx)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"⚠️ Officer notification failed (clearance succeeded): {nEx.Message}");
                }

                LoadAssignedEquipment();
                SelectedEquipment = null;
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ============================================================
        // MARK ALL RETURNED
        // ------------------------------------------------------------
        // Uses local copies of SelectedOfficer / App.CurrentUser so the
        // nullable flow analysis survives the awaits inside the loop.
        // Damaged items keep their status for the disposal pipeline.
        //
        // NEW: single notification to the officer summarizing the sweep.
        // ============================================================
        private async void OnMarkAllReturned()
        {
            var officer = SelectedOfficer;
            var currentUser = App.CurrentUser;

            if (officer == null)
            {
                StatusMessage = "Please select an officer first.";
                return;
            }

            if (AssignedEquipment.Count == 0)
            {
                StatusMessage = "No equipment to return.";
                return;
            }

            bool confirm = await Shell.Current.DisplayAlert(
                "Confirm All Returns",
                $"Mark all {AssignedEquipment.Count} items as returned?\n\n" +
                "Damaged items will only have their assignment cleared — " +
                "they will stay in the disposal pipeline.",
                "Yes",
                "Cancel");

            if (!confirm) return;

            IsBusy = true;
            StatusMessage = string.Empty;

            try
            {
                int returnedCount = 0;
                int damagedSkipped = 0;

                foreach (var eq in AssignedEquipment.ToList())
                {
                    if (string.IsNullOrEmpty(eq.AssignedToUsername)) continue;

                    // ✅ Close the Assignments row before touching Equipment
                    await _db.CloseActiveAssignmentsAsync(eq.EquipmentId, officer.Username);

                    string originalStatus = eq.Status;
                    eq.AssignedToUsername = null;
                    eq.LastUpdated = DateTime.Now;

                    if (originalStatus == "Damaged" || originalStatus == "InRepair")
                    {
                        damagedSkipped++;
                        // Status stays as Damaged/InRepair
                    }
                    else
                    {
                        eq.Status = "Available";
                        returnedCount++;
                    }

                    await _db.SaveEquipmentAsync(eq);

                    var transaction = new TransactionModel
                    {
                        EquipmentQR = eq.QRCode,
                        FromUser = officer.Username,
                        ToUser = currentUser?.Username ?? "admin",
                        Timestamp = DateTime.Now,
                        Action = "Return",
                        Remarks = originalStatus == "Damaged" || originalStatus == "InRepair"
                            ? $"Assignment cleared during clearance (status: {originalStatus}). Disposal pending."
                            : "Marked all returned during clearance."
                    };
                    await _db.SaveTransactionAsync(transaction);
                }

                if (currentUser != null)
                {
                    await _db.LogActionAsync(
                        currentUser.Username,
                        "Clearance All Returned",
                        $"Marked {returnedCount} item(s) as returned; " +
                        $"{damagedSkipped} damaged item(s) kept in pipeline for {officer.FullName}");
                }

                // ✅ NEW: single sweep notification to the officer
                try
                {
                    await _db.SendNotificationAsync(
                        officer.Username,
                        "✅ Clearance Complete",
                        $"{returnedCount} item(s) cleared from your accountability" +
                        (damagedSkipped > 0
                            ? $"; {damagedSkipped} damaged item(s) pending disposal."
                            : "."));
                }
                catch (Exception nEx)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"⚠️ Officer notification failed (clearance succeeded): {nEx.Message}");
                }

                if (damagedSkipped > 0)
                {
                    StatusMessage = $"✅ {returnedCount} returned; " +
                                    $"{damagedSkipped} damaged item(s) kept for disposal.";
                }
                else
                {
                    StatusMessage = $"✅ All {returnedCount} items marked as returned.";
                }

                LoadAssignedEquipment();
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ============================================================
        // GENERATE CLEARANCE CERTIFICATE
        // ============================================================
        private async void OnGenerateCertificate()
        {
            if (SelectedOfficer == null)
            {
                StatusMessage = "Please select an officer first.";
                return;
            }

            var outstanding = AssignedEquipment
                .Where(e => !string.IsNullOrEmpty(e.AssignedToUsername))
                .ToList();

            if (outstanding.Any())
            {
                bool confirm = await Shell.Current.DisplayAlert(
                    "Outstanding Equipment",
                    $"{outstanding.Count} item(s) still have assignments. Mark them as returned now?",
                    "Yes, Mark All",
                    "Cancel");

                if (confirm)
                {
                    OnMarkAllReturned();
                    return;
                }
                else
                {
                    return;
                }
            }

            IsBusy = true;
            StatusMessage = string.Empty;

            try
            {
                var pdfService = new PdfGenerationService();
                var allItems = AssignedEquipment.ToList();
                var pdfBytes = pdfService.GenerateClearanceCertificate(SelectedOfficer, allItems);

                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    StatusMessage = "❌ PDF generation returned empty data.";
                    IsBusy = false;
                    return;
                }

                var fileName = $"Clearance_{SelectedOfficer.Username}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                var downloadsPath = Path.Combine(FileSystem.AppDataDirectory, "Clearance");
                Directory.CreateDirectory(downloadsPath);
                var filePath = Path.Combine(downloadsPath, fileName);
                await File.WriteAllBytesAsync(filePath, pdfBytes);

                try
                {
                    await Launcher.Default.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(filePath) });
                }
                catch
                {
                    await Share.Default.RequestAsync(new ShareFileRequest
                    {
                        Title = "View Clearance Certificate",
                        File = new ShareFile(filePath)
                    });
                }

                StatusMessage = $"✅ Clearance certificate generated for {SelectedOfficer.FullName}";

                LoadAssignedEquipment();
                SelectedEquipment = null;
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Certificate generation failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OnRefresh()
        {
            RefreshAsync();
        }
    }
}