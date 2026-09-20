using Firetrack.Models;
using Firetrack.Services;
using Firetrack.Helpers;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using ZXing.Net.Maui;

namespace Firetrack.ViewModels
{
    public class ScannerViewModel : ViewModelBase
    {
        private readonly DatabaseService? _db;
        private bool _isScanning;
        private string _scanResult = string.Empty;
        private EquipmentModel? _foundEquipment;
        private bool _isBusy;
        private string _returnToPage = string.Empty;
        private string _scanMode = "equipment";

        // Controls camera/placeholder visibility safely via MVVM
        private bool _isCameraReady = false;
        private bool _isPlaceholderVisible = true;

        public bool IsScanning
        {
            get => _isScanning;
            set { _isScanning = value; OnPropertyChanged(); }
        }

        public string ScanResult
        {
            get => _scanResult;
            set { _scanResult = value; OnPropertyChanged(); }
        }

        public EquipmentModel? FoundEquipment
        {
            get => _foundEquipment;
            set { _foundEquipment = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public string ReturnToPage
        {
            get => _returnToPage;
            set { _returnToPage = value; OnPropertyChanged(); }
        }

        public string ScanMode
        {
            get => _scanMode;
            set { _scanMode = value; OnPropertyChanged(); }
        }

        public bool IsCameraReady
        {
            get => _isCameraReady;
            set { _isCameraReady = value; OnPropertyChanged(); }
        }

        public bool IsPlaceholderVisible
        {
            get => _isPlaceholderVisible;
            set { _isPlaceholderVisible = value; OnPropertyChanged(); }
        }

        public ICommand CancelCommand { get; }

        public ScannerViewModel()
        {
            _db = App.Database;
            IsScanning = true;
            CancelCommand = new Command(OnCancel);
        }

        private async void OnCancel()
        {
            try
            {
                // If we were pushed onto a stack, pop back naturally.
                if (Shell.Current.Navigation.NavigationStack.Count > 1)
                {
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                // We're a root page (opened from flyout) — no stack to pop.
                var user = App.CurrentUser;
                string dashboardRoute = user?.Role == "Admin"
                    ? "//AdminDashboard"
                    : "//PersonnelDashboard";
                await Shell.Current.GoToAsync(dashboardRoute);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"❌ Scanner cancel navigation failed: {ex.Message}");
                try { await Shell.Current.GoToAsync(".."); } catch { /* last resort */ }
            }
        }

        // ============================================================
        // PROCESS SCANNED QR
        // ------------------------------------------------------------
        // Called from two places:
        //   1. OnBarcodesDetected (hardware scan — MediaTek often fails)
        //   2. OnTypeQrClicked  (manual entry fallback — works everywhere)
        //
        // Two modes of operation:
        //   A. "Return to caller" — ReturnToPage is set (Transfer flow).
        //      We do NOT do a DB lookup here; the caller
        //      (TransferViewModel.ProcessScannedQR) handles the lookup
        //      because it needs to distinguish equipment vs person QR.
        //   B. "Standalone" — no ReturnToPage. We do the equipment lookup
        //      and display the result inline.
        //
        // FIXES APPLIED:
        //  • Audit log entry on successful equipment lookup.
        //  • Audit log entry when handing off to a caller page.
        // ============================================================
        public async Task ProcessScannedQR(string qrValue)
        {
            if (_db == null)
            {
                await Shell.Current.DisplayAlert("Error", "Database not available.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(qrValue))
                return;

            IsBusy = true;
            ScanResult = $"Scanned: {qrValue}";

            try
            {
                // ---------- MODE A: Hand off to caller ----------
                if (!string.IsNullOrEmpty(ReturnToPage))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"🔍 Scanner handing off to {ReturnToPage} — qr={qrValue}, mode={ScanMode}");

                    // ✅ Audit log the scan event so it shows on AuditLogPage
                    if (App.CurrentUser != null)
                    {
                        await _db.LogActionAsync(
                            App.CurrentUser.Username,
                            "Scan QR",
                            $"Scanned '{qrValue}' (mode={ScanMode})");
                    }

                    var navParams = new Dictionary<string, object>
                    {
                        { "scannedQR", qrValue },
                        { "mode", ScanMode }
                    };

                    // Pop back to the caller (TransferPage)
                    await Shell.Current.GoToAsync("..", navParams);
                    return;
                }

                // ---------- MODE B: Standalone lookup ----------
                var equipmentList = await _db.GetEquipmentsAsync();
                var found = equipmentList.FirstOrDefault(e => e.QRCode == qrValue);

                if (found != null)
                {
                    FoundEquipment = found;

                    // ✅ Audit log successful scan
                    if (App.CurrentUser != null)
                    {
                        await _db.LogActionAsync(
                            App.CurrentUser.Username,
                            "Scan QR",
                            $"Scanned '{found.Name}' ({found.QRCode})");
                    }

                    await Shell.Current.DisplayAlert(
                        "Equipment Found",
                        $"Name: {found.Name}\n" +
                        $"Type: {found.Type}\n" +
                        $"Status: {found.Status}\n" +
                        $"Assigned to: {found.AssignedToUsername ?? "None"}",
                        "OK");
                }
                else
                {
                    await Shell.Current.DisplayAlert(
                        "Not Found",
                        "No equipment matches this QR code.",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ProcessScannedQR error: {ex}");
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
                await Task.Delay(1500);
                IsScanning = true;
                ScanResult = string.Empty;
            }
        }

        public void ResumeScanning()
        {
            IsScanning = true;
            FoundEquipment = null;
            ScanResult = string.Empty;
        }
    }
}