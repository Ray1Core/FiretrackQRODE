using Firetrack.Models;
using Firetrack.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class TransferViewModel : ViewModelBase
    {
        private readonly DatabaseService? _db;
        private UserModel? _selectedOfficer;
        private EquipmentModel? _selectedEquipment;
        private string _manualEquipmentQR = string.Empty;
        private bool _isBusy;

        public ObservableCollection<UserModel> Users { get; set; } = new();
        public ObservableCollection<EquipmentModel> EquipmentList { get; set; } = new();

        public UserModel? SelectedOfficer
        {
            get => _selectedOfficer;
            set { _selectedOfficer = value; OnPropertyChanged(); }
        }

        public EquipmentModel? SelectedEquipment
        {
            get => _selectedEquipment;
            set { _selectedEquipment = value; OnPropertyChanged(); }
        }

        public string ManualEquipmentQR
        {
            get => _manualEquipmentQR;
            set { _manualEquipmentQR = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand TransferCommand { get; }

        public TransferViewModel()
        {
            if (App.CurrentUser?.Role != "Admin")
            {
                TransferCommand = new Command(() => { });
                return;
            }

            _db = App.Database;
            if (_db == null)
            {
                TransferCommand = new Command(() => Shell.Current.DisplayAlert("Error", "Database not available.", "OK"));
                return;
            }

            TransferCommand = new Command(OnTransfer);
            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            if (_db == null) return;

            IsBusy = true;
            try
            {
                var userList = await _db.GetUsersAsync();
                Users.Clear();
                foreach (var u in userList)
                    Users.Add(u);

                var eqList = await _db.GetEquipmentsAsync();
                EquipmentList.Clear();
                foreach (var eq in eqList)
                    EquipmentList.Add(eq);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void OnTransfer()
        {
            if (_db == null)
            {
                await Shell.Current.DisplayAlert("Error", "Database not available.", "OK");
                return;
            }

            if (SelectedOfficer == null)
            {
                await Shell.Current.DisplayAlert("Validation", "Please select the receiving officer.", "OK");
                return;
            }

            EquipmentModel? equipment = SelectedEquipment;
            if (equipment == null)
            {
                await Shell.Current.DisplayAlert("Validation", "Please select an equipment.", "OK");
                return;
            }

            IsBusy = true;
            try
            {
                var capturedOfficer = SelectedOfficer;
                var capturedEquipment = equipment;

                var transaction = new TransactionModel
                {
                    EquipmentQR = capturedEquipment.QRCode,
                    FromUser = App.CurrentUser?.Username ?? "admin",
                    ToUser = capturedOfficer.Username,
                    Timestamp = DateTime.Now,
                    Action = "Issue",
                    Remarks = $"Issued to {capturedOfficer.FullName}"
                };

                capturedEquipment.AssignedToUsername = capturedOfficer.Username;
                capturedEquipment.Status = "Issued";
                capturedEquipment.LastUpdated = DateTime.Now;

                await _db.SaveTransactionAsync(transaction);
                await _db.SaveEquipmentAsync(capturedEquipment);

                // ✅ Log the action
                if (App.CurrentUser != null)
                {
                    await _db.LogActionAsync(
                        App.CurrentUser.Username,
                        "Transfer/Issue",
                        $"Issued '{capturedEquipment.Name}' to {capturedOfficer.Username}");
                }

                await _db.SendNotificationAsync(
                    capturedOfficer.Username,
                    "🔄 Equipment Issued",
                    $"{App.CurrentUser?.FullName} issued '{capturedEquipment.Name}' to you."
                );

                await Shell.Current.DisplayAlert("Success", $"Equipment '{capturedEquipment.Name}' issued to {capturedOfficer.FullName}.", "OK");

                SelectedEquipment = null;
                SelectedOfficer = null;

                await LoadDataAsync();

                var navParams = new Dictionary<string, object>
                {
                    { "equipment", capturedEquipment },
                    { "officer", capturedOfficer }
                };
                await Shell.Current.GoToAsync("IcsPage", navParams);
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}