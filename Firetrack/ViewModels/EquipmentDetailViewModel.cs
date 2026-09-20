using Firetrack.Models;
using Firetrack.Services;
using Firetrack.Helpers;
using System;
using System.IO;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace Firetrack.ViewModels
{
    public class EquipmentDetailViewModel : ViewModelBase
    {
        private readonly DatabaseService? _db;
        private EquipmentModel _equipment = null!;
        private bool _isBusy;
        private ImageSource? _qrImage;

        public EquipmentModel Equipment
        {
            get => _equipment;
            set { _equipment = value; OnPropertyChanged(); }
        }

        public ImageSource? QrImage
        {
            get => _qrImage;
            set { _qrImage = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand EditCommand { get; }
        public ICommand HistoryCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand DownloadQrCommand { get; }

        public EquipmentDetailViewModel(EquipmentModel equipment)
        {
            _db = App.Database;
            Equipment = equipment;

            if (!string.IsNullOrEmpty(Equipment.QRCode))
                QrImage = QrHelper.GenerateImageSource(Equipment.QRCode);

            EditCommand = new Command(OnEdit);
            HistoryCommand = new Command(OnHistory);
            DeleteCommand = new Command(OnDelete);
            DownloadQrCommand = new Command(OnDownloadQr);
        }

        // ---- Download QR as PNG + share/open ----
        private async void OnDownloadQr()
        {
            if (string.IsNullOrEmpty(Equipment.QRCode))
            {
                await Shell.Current.DisplayAlert("Error", "This equipment has no QR code.", "OK");
                return;
            }

            IsBusy = true;
            try
            {
                // 1. Generate PNG bytes
                var pngBytes = QrHelper.GeneratePng(Equipment.QRCode, 20);
                if (pngBytes.Length == 0)
                {
                    await Shell.Current.DisplayAlert("Error", "QR generation failed.", "OK");
                    return;
                }

                // 2. Save to app data directory
                var safeName = MakeSafeFileName(Equipment.QRCode);
                var folder = Path.Combine(FileSystem.AppDataDirectory, "EquipmentQR");
                Directory.CreateDirectory(folder);
                var filePath = Path.Combine(folder, $"{safeName}.png");
                await File.WriteAllBytesAsync(filePath, pngBytes);

                // 3. Try to open with default viewer (Windows Explorer / Android viewer)
                try
                {
                    await Launcher.Default.OpenAsync(new OpenFileRequest
                    {
                        File = new ReadOnlyFile(filePath)
                    });
                }
                catch
                {
                    // 4. Fall back to Share sheet — user can send to printer app, save to Downloads, etc.
                    await Share.Default.RequestAsync(new ShareFileRequest
                    {
                        Title = $"QR Code - {Equipment.Name}",
                        File = new ShareFile(filePath)
                    });
                }

                await Shell.Current.DisplayAlert("Success",
                    $"QR code saved:\n{filePath}\n\nPrint this and stick it on '{Equipment.Name}'.",
                    "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Failed to save QR: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static string MakeSafeFileName(string input)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                input = input.Replace(c, '_');
            return input;
        }

        private async void OnEdit()
        {
            if (_db == null) { await Shell.Current.DisplayAlert("Error", "Database not available.", "OK"); return; }

            string newName = await Shell.Current.DisplayPromptAsync(
                "Edit Equipment", $"Current name: {Equipment.Name}\nEnter new name:",
                "Save", "Cancel", placeholder: Equipment.Name);
            if (newName == null) return;
            if (!string.IsNullOrWhiteSpace(newName) && newName != Equipment.Name)
                Equipment.Name = newName.Trim();

            string newStatus = await Shell.Current.DisplayActionSheet(
                "Select Status", "Cancel", null,
                "Available", "Issued", "Damaged", "InRepair", "Disposed");
            if (!string.IsNullOrEmpty(newStatus) && newStatus != "Cancel")
                Equipment.Status = newStatus;

            try
            {
                IsBusy = true;
                await _db.SaveEquipmentAsync(Equipment);
                await Shell.Current.DisplayAlert("Success", "Equipment updated.", "OK");
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex) { await Shell.Current.DisplayAlert("Error", ex.Message, "OK"); }
            finally { IsBusy = false; }
        }

        private async void OnHistory()
        {
            var navParams = new Dictionary<string, object> { { "equipment", Equipment } };
            await Shell.Current.GoToAsync(Routes.TransactionHistory, navParams);
        }

        private async void OnDelete()
        {
            if (_db == null) { await Shell.Current.DisplayAlert("Error", "Database not available.", "OK"); return; }

            bool confirm = await Shell.Current.DisplayAlert(
                "Confirm Delete", $"Delete '{Equipment.Name}'?", "Yes", "Cancel");
            if (!confirm) return;

            try
            {
                IsBusy = true;
                await _db.DeleteEquipmentAsync(Equipment);
                await Shell.Current.DisplayAlert("Success", "Equipment deleted.", "OK");
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex) { await Shell.Current.DisplayAlert("Error", ex.Message, "OK"); }
            finally { IsBusy = false; }
        }
    }
}