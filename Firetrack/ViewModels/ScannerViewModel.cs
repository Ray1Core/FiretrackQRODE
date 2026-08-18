using Firetrack.Models;
using Firetrack.Services;
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
        private string _scanMode = "equipment";   // NEW: default mode

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

        // ✅ NEW: Scan mode (e.g., "equipment", "personnel", "admin")
        public string ScanMode
        {
            get => _scanMode;
            set { _scanMode = value; OnPropertyChanged(); }
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
            if (!string.IsNullOrEmpty(ReturnToPage))
                await Shell.Current.GoToAsync($"//{ReturnToPage}");
            else
                await Shell.Current.GoToAsync("//DashboardPage");
        }

        public async Task ProcessScannedQR(string qrValue)
        {
            if (_db == null)
            {
                await Shell.Current.DisplayAlert("Error", "Database not available.", "OK");
                return;
            }

            IsBusy = true;
            ScanResult = $"Scanned: {qrValue}";

            try
            {
                var equipmentList = await _db.GetEquipmentsAsync();
                var found = equipmentList.FirstOrDefault(e => e.QRCode == qrValue);

                // If we are returning to a caller, pass the QR and mode back
                if (!string.IsNullOrEmpty(ReturnToPage))
                {
                    var navParams = new Dictionary<string, object>
                    {
                        { "scannedQR", qrValue },
                        { "mode", ScanMode }   // ✅ Pass mode back
                    };
                    await Shell.Current.GoToAsync($"..", navParams);
                    return;
                }

                // Standalone mode: show equipment details
                if (found != null)
                {
                    FoundEquipment = found;
                    await Shell.Current.DisplayAlert(
                        "Equipment Found",
                        $"Name: {found.Name}\nType: {found.Type}\nStatus: {found.Status}\nAssigned to: {found.AssignedToUsername ?? "None"}",
                        "OK");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Not Found", "No equipment matches this QR code.", "OK");
                }
            }
            catch (Exception ex)
            {
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