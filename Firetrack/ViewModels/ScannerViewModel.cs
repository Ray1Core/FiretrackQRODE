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

        public ICommand ScanCommand { get; }
        // GoBackCommand removed – navigation handled by Shell

        public ScannerViewModel()
        {
            _db = App.Database;
            IsScanning = true;
            ScanCommand = new Command<BarcodeResult>(OnScanResult);
            // GoBackCommand assignment removed
        }

        private async void OnScanResult(BarcodeResult? result)
        {
            if (result == null || string.IsNullOrEmpty(result.Value))
                return;

            if (_db == null)
            {
                await Shell.Current.DisplayAlert("Error", "Database not available.", "OK");
                return;
            }

            IsScanning = false;
            ScanResult = $"Scanned: {result.Value}";

            var equipmentList = await _db.GetEquipmentsAsync();
            var found = equipmentList.FirstOrDefault(e => e.QRCode == result.Value);

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
                await Task.Delay(2000);
                IsScanning = true;
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