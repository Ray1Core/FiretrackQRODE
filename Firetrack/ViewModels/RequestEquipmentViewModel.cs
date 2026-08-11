using Firetrack.Models;
using Firetrack.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class RequestEquipmentViewModel : ViewModelBase
    {
        private ObservableCollection<EquipmentModel> _availableEquipment = new();
        private EquipmentModel? _selectedEquipment;
        private bool _isBusy;

        public ObservableCollection<EquipmentModel> AvailableEquipment
        {
            get => _availableEquipment;
            set { _availableEquipment = value; OnPropertyChanged(); }
        }

        public EquipmentModel? SelectedEquipment
        {
            get => _selectedEquipment;
            set { _selectedEquipment = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand LoadAvailableCommand { get; }
        public ICommand RequestCommand { get; }
        // GoBackCommand removed – navigation handled by Shell

        public RequestEquipmentViewModel()
        {
            LoadAvailableCommand = new Command(async () => await OnLoadAvailable());
            RequestCommand = new Command(OnRequest);
            // GoBackCommand assignment removed
            _ = OnLoadAvailable();
        }

        private async Task OnLoadAvailable()
        {
            if (App.Database == null) return;

            IsBusy = true;
            try
            {
                var all = await App.Database.GetEquipmentsAsync();
                // Filter: Available, not assigned, not pending request
                var available = all.Where(e =>
                    e.Status == "Available" &&
                    string.IsNullOrEmpty(e.AssignedToUsername) &&
                    string.IsNullOrEmpty(e.RequestStatus) // only truly available
                );
                AvailableEquipment.Clear();
                foreach (var item in available)
                    AvailableEquipment.Add(item);
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

        private async void OnRequest()
        {
            if (SelectedEquipment == null)
            {
                await Shell.Current.DisplayAlert("Validation", "Please select an equipment first.", "OK");
                return;
            }

            if (App.CurrentUser == null)
            {
                await Shell.Current.DisplayAlert("Error", "You must be logged in.", "OK");
                return;
            }

            bool confirm = await Shell.Current.DisplayAlert(
                "Confirm Request",
                $"Request '{SelectedEquipment.Name}'?",
                "Yes",
                "Cancel");

            if (!confirm) return;

            IsBusy = true;
            try
            {
                var equipment = SelectedEquipment;
                equipment.RequestedByUsername = App.CurrentUser.Username;
                equipment.RequestStatus = "Pending";
                equipment.LastUpdated = DateTime.Now;

                await App.Database!.SaveEquipmentAsync(equipment);

                // Notify admin
                await App.Database!.SendNotificationAsync(
                    "admin",
                    "📋 New Equipment Request",
                    $"{App.CurrentUser.FullName} requested '{equipment.Name}'.");

                await Shell.Current.DisplayAlert("Success", $"Request for '{equipment.Name}' submitted for approval.", "OK");
                SelectedEquipment = null;
                await OnLoadAvailable(); // refresh list
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