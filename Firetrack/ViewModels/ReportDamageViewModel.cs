using Firetrack.Models;
using Firetrack.Services;
using Firetrack.Helpers;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace Firetrack.ViewModels
{
    public class ReportDamageViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private EquipmentModel? _equipment;
        private string _remarks = string.Empty;
        private string _photoPath = string.Empty;
        private ImageSource? _photoPreview;
        private bool _isBusy;
        private bool _isSubmitting;   // ✅ prevents double-submit

        public EquipmentModel Equipment
        {
            get => _equipment!;
            set { _equipment = value; OnPropertyChanged(); }
        }

        public string Remarks
        {
            get => _remarks;
            set { _remarks = value; OnPropertyChanged(); }
        }

        public string PhotoPath
        {
            get => _photoPath;
            set { _photoPath = value; OnPropertyChanged(); }
        }

        public ImageSource? PhotoPreview
        {
            get => _photoPreview;
            set { _photoPreview = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand PickPhotoCommand { get; }
        public ICommand SubmitReportCommand { get; }

        public ReportDamageViewModel(EquipmentModel equipment)
        {
            _db = App.Database!;
            Equipment = equipment;

            PickPhotoCommand = new Command(OnPickPhoto);
            SubmitReportCommand = new Command(OnSubmitReport);
        }

        // ============================================================
        // PICK PHOTO (unchanged logic — just kept tidy)
        // ============================================================
        private async void OnPickPhoto()
        {
            try
            {
                var photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "Pick a photo of the damaged equipment"
                });

                if (photo == null) return;

                IsBusy = true;

                var localPath = await SavePhotoAsync(photo);
                if (!string.IsNullOrEmpty(localPath))
                {
                    PhotoPath = localPath;
                    PhotoPreview = ImageSource.FromFile(localPath);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Error picking photo: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task<string> SavePhotoAsync(FileResult photo)
        {
            var fileName = $"damage_{Equipment.QRCode}_{DateTime.Now:yyyyMMddHHmmss}.jpg";
            var appDataDir = FileSystem.AppDataDirectory;
            var savePath = Path.Combine(appDataDir, "DamagePhotos");

            if (!Directory.Exists(savePath))
                Directory.CreateDirectory(savePath);

            var fullPath = Path.Combine(savePath, fileName);

            using var stream = await photo.OpenReadAsync();
            using var fileStream = File.Create(fullPath);
            await stream.CopyToAsync(fileStream);

            return fullPath;
        }

        // ============================================================
        // SUBMIT DAMAGE REPORT
        // ------------------------------------------------------------
        // Full end-to-end flow:
        //   1. Validate input (photo + remarks)
        //   2. Mark equipment as Damaged, save
        //   3. Write a TransactionModel row (shows on TransactionHistory)
        //   4. Audit-log the damage report
        //   5. Notify admin@firetrack.gov
        //   6. ✅ Auto-create a DisposalRequest so the item enters the
        //      admin's disposal queue in the same action
        //   7. ✅ Audit-log the auto disposal request too (NEW)
        //   8. Navigate back to dashboard
        //
        // FIXES APPLIED:
        //   • Double-submit guard (_isSubmitting)
        //   • Rollback in-memory Status if DB write fails
        //   • Audit-log the auto-created disposal request
        // ============================================================
        private async void OnSubmitReport()
        {
            // ✅ Guard: prevent double-tap during the async save
            if (_isSubmitting) return;

            if (string.IsNullOrWhiteSpace(PhotoPath))
            {
                await Shell.Current.DisplayAlert(
                    "Validation",
                    "Please take or select a photo of the damage.",
                    "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(Remarks))
            {
                await Shell.Current.DisplayAlert(
                    "Validation",
                    "Please add remarks describing the damage.",
                    "OK");
                return;
            }

            if (App.CurrentUser == null)
            {
                await Shell.Current.DisplayAlert(
                    "Error",
                    "You must be logged in to submit a damage report.",
                    "OK");
                return;
            }

            _isSubmitting = true;
            IsBusy = true;

            // ✅ Snapshot the previous status so we can roll back
            //    the in-memory model if the DB write fails.
            string previousStatus = Equipment.Status;
            string? previousPhotoPath = Equipment.PhotoPath;
            string? previousRemarks = Equipment.Remarks;

            try
            {
                // ---------- 1. Update the equipment row ----------
                Equipment.Status = "Damaged";
                Equipment.PhotoPath = PhotoPath;
                Equipment.Remarks = Remarks;
                Equipment.LastUpdated = DateTime.Now;

                await _db.SaveEquipmentAsync(Equipment);

                // ---------- 2. Log as a transaction (chain of custody) ----------
                var transaction = new TransactionModel
                {
                    EquipmentQR = Equipment.QRCode,
                    FromUser = App.CurrentUser.Username,
                    ToUser = Equipment.AssignedToUsername ?? "none",
                    Timestamp = DateTime.Now,
                    Action = "ReportDamage",
                    Remarks = Remarks
                };
                await _db.SaveTransactionAsync(transaction);

                // ---------- 3. Audit log the damage report ----------
                await _db.LogActionAsync(
                    App.CurrentUser.Username,
                    "Report Damage",
                    $"Reported damage on '{Equipment.Name}' ({Equipment.QRCode}). Remarks: {Remarks}");

                // ---------- 4. Notify admin ----------
                await _db.SendNotificationAsync(
                    "admin@firetrack.gov",   // ✅ correct admin email
                    "⚠️ Damage Report",
                    $"{App.CurrentUser.FullName} reported damage on '{Equipment.Name}' ({Equipment.QRCode}).");

                // ---------- 5. Auto-create the disposal request ----------
                bool disposalRequested = await _db.RequestDisposalAsync(
                    Equipment.QRCode,
                    App.CurrentUser.Username,
                    string.IsNullOrWhiteSpace(Remarks)
                        ? "Damaged equipment reported by personnel"
                        : Remarks);

                System.Diagnostics.Debug.WriteLine(
                    $"🗑️ Auto-created disposal request: {disposalRequested}");

                // ✅ NEW: Audit-log the auto disposal request so it
                //    shows up on AuditLogPage as its own action.
                if (disposalRequested)
                {
                    await _db.LogActionAsync(
                        App.CurrentUser.Username,
                        "Request Disposal",
                        $"Auto-created disposal request for '{Equipment.Name}' ({Equipment.QRCode}) via damage report.");
                }

                // ---------- 6. Confirmation ----------
                string message = disposalRequested
                    ? "Damage report submitted successfully.\n\n" +
                      "The Admin has been notified and a disposal request is now pending approval."
                    : "Damage report submitted successfully.\n\n" +
                      "The Admin has been notified.";

                await Shell.Current.DisplayAlert("Success", message, "OK");

                await Shell.Current.GoToAsync(Routes.GetDashboardRoute());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Damage report failed: {ex}");

                // ✅ Roll back the in-memory model so the UI doesn't
                //    show the item as Damaged when the DB write failed.
                Equipment.Status = previousStatus;
                Equipment.PhotoPath = previousPhotoPath;
                Equipment.Remarks = previousRemarks;

                await Shell.Current.DisplayAlert(
                    "Error",
                    $"Could not submit damage report: {ex.Message}",
                    "OK");
            }
            finally
            {
                IsBusy = false;
                _isSubmitting = false;
            }
        }
    }
}