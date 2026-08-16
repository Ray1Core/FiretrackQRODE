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

        // Commands
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

        // ---- Load Officers ----
        private async void LoadOfficers()
        {
            var users = await _db.GetUsersAsync();
            Officers.Clear();
            foreach (var u in users.Where(u => u.Role == "Personnel"))
                Officers.Add(u);
        }

        // ---- Load Assigned Equipment ----
        private async void LoadAssignedEquipment()
        {
            if (SelectedOfficer == null)
            {
                AssignedEquipment.Clear();
                return;
            }

            IsBusy = true;
            var equipment = await _db.GetEquipmentsAssignedToUserAsync(SelectedOfficer.Username);
            AssignedEquipment.Clear();
            foreach (var eq in equipment)
                AssignedEquipment.Add(eq);
            IsBusy = false;
        }

        // ---- Mark Single Equipment as Returned ----
        private async void OnMarkReturned()
        {
            if (SelectedEquipment == null)
            {
                StatusMessage = "Please select an equipment to mark as returned.";
                return;
            }

            if (SelectedEquipment.Status == "Available" && string.IsNullOrEmpty(SelectedEquipment.AssignedToUsername))
            {
                StatusMessage = "This equipment is already marked as returned.";
                return;
            }

            IsBusy = true;
            StatusMessage = string.Empty;

            try
            {
                var transaction = new TransactionModel
                {
                    EquipmentQR = SelectedEquipment.QRCode,
                    FromUser = SelectedEquipment.AssignedToUsername ?? "unknown",
                    ToUser = App.CurrentUser?.Username ?? "admin",
                    Timestamp = DateTime.Now,
                    Action = "Return",
                    Remarks = $"Returned by {SelectedEquipment.AssignedToUsername} during clearance."
                };

                SelectedEquipment.AssignedToUsername = null;
                SelectedEquipment.Status = "Available";
                SelectedEquipment.LastUpdated = DateTime.Now;

                await _db.SaveTransactionAsync(transaction);
                await _db.SaveEquipmentAsync(SelectedEquipment);

                if (App.CurrentUser != null)
                {
                    await _db.LogActionAsync(
                        App.CurrentUser.Username,
                        "Clearance Return",
                        $"Marked '{SelectedEquipment.Name}' as returned from {SelectedEquipment.AssignedToUsername ?? "unknown"}");
                }

                StatusMessage = $"✅ {SelectedEquipment.Name} marked as returned.";
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

        // ---- Mark All Equipment as Returned ----
        private async void OnMarkAllReturned()
        {
            if (SelectedOfficer == null)
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
                $"Mark all {AssignedEquipment.Count} items as returned?",
                "Yes",
                "Cancel");
            if (!confirm) return;

            IsBusy = true;
            StatusMessage = string.Empty;

            try
            {
                foreach (var eq in AssignedEquipment.ToList())
                {
                    if (eq.Status == "Issued" && !string.IsNullOrEmpty(eq.AssignedToUsername))
                    {
                        eq.AssignedToUsername = null;
                        eq.Status = "Available";
                        eq.LastUpdated = DateTime.Now;
                        await _db.SaveEquipmentAsync(eq);

                        var transaction = new TransactionModel
                        {
                            EquipmentQR = eq.QRCode,
                            FromUser = SelectedOfficer.Username,
                            ToUser = App.CurrentUser?.Username ?? "admin",
                            Timestamp = DateTime.Now,
                            Action = "Return",
                            Remarks = $"Marked all returned during clearance."
                        };
                        await _db.SaveTransactionAsync(transaction);
                    }
                }

                if (App.CurrentUser != null)
                {
                    await _db.LogActionAsync(
                        App.CurrentUser.Username,
                        "Clearance All Returned",
                        $"Marked all {AssignedEquipment.Count} items as returned for {SelectedOfficer.FullName}");
                }

                StatusMessage = $"✅ All {AssignedEquipment.Count} items marked as returned.";
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

        // ---- Generate Clearance Certificate ----
        private async void OnGenerateCertificate()
        {
            if (SelectedOfficer == null)
            {
                StatusMessage = "Please select an officer first.";
                return;
            }

            // First, ensure all equipment is returned
            var issuedItems = AssignedEquipment.Where(e => e.Status == "Issued").ToList();
            if (issuedItems.Any())
            {
                bool confirm = await Shell.Current.DisplayAlert(
                    "Outstanding Equipment",
                    $"{issuedItems.Count} item(s) are still issued. Mark them as returned now?",
                    "Yes, Mark All",
                    "Cancel");
                if (confirm)
                {
                    // Mark all issued items as returned
                    IsBusy = true;
                    try
                    {
                        foreach (var eq in issuedItems)
                        {
                            eq.AssignedToUsername = null;
                            eq.Status = "Available";
                            eq.LastUpdated = DateTime.Now;
                            await _db.SaveEquipmentAsync(eq);

                            var transaction = new TransactionModel
                            {
                                EquipmentQR = eq.QRCode,
                                FromUser = SelectedOfficer.Username,
                                ToUser = App.CurrentUser?.Username ?? "admin",
                                Timestamp = DateTime.Now,
                                Action = "Return",
                                Remarks = $"Auto-returned before clearance certificate."
                            };
                            await _db.SaveTransactionAsync(transaction);
                        }
                        LoadAssignedEquipment();
                        StatusMessage = $"✅ {issuedItems.Count} item(s) marked as returned.";
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = $"❌ Error returning items: {ex.Message}";
                        IsBusy = false;
                        return;
                    }
                    IsBusy = false;
                }
                else
                {
                    return;
                }
            }

            // Now generate the certificate (assumes all are returned)
            IsBusy = true;
            StatusMessage = string.Empty;

            try
            {
                var pdfService = new PdfGenerationService();
                var allItems = AssignedEquipment.ToList(); // all should be returned
                var pdfBytes = pdfService.GenerateClearanceCertificate(SelectedOfficer, allItems);

                var fileName = $"Clearance_{SelectedOfficer.Username}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                var downloadsPath = Path.Combine(FileSystem.AppDataDirectory, "Clearance");
                if (!Directory.Exists(downloadsPath))
                    Directory.CreateDirectory(downloadsPath);

                var filePath = Path.Combine(downloadsPath, fileName);
                await File.WriteAllBytesAsync(filePath, pdfBytes);

                // Open the PDF
                await Launcher.Default.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(filePath)
                });

                StatusMessage = $"✅ Clearance certificate generated for {SelectedOfficer.FullName}";
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

        // ---- Refresh ----
        private void OnRefresh()
        {
            LoadOfficers();
            if (SelectedOfficer != null)
                LoadAssignedEquipment();
        }
    }
}