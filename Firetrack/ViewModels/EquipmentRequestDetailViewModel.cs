using Firetrack.Models;
using Firetrack.Services;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class EquipmentRequestDetailViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private EquipmentModel _equipment = null!;
        private bool _isBusy;

        public EquipmentModel Equipment
        {
            get => _equipment;
            set { _equipment = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand RequestCommand { get; }

        public EquipmentRequestDetailViewModel(EquipmentModel equipment)
        {
            _db = App.Database!;
            Equipment = equipment;
            RequestCommand = new Command(OnRequest);
        }

        private async void OnRequest()
        {
            if (App.CurrentUser == null)
            {
                await Shell.Current.DisplayAlert("Error", "You must be logged in.", "OK");
                return;
            }

            bool confirm = await Shell.Current.DisplayAlert(
                "Confirm Request",
                $"Request '{Equipment.Name}'?",
                "Yes",
                "Cancel");

            if (!confirm) return;

            IsBusy = true;
            try
            {
                Equipment.RequestedByUsername = App.CurrentUser.Username;
                Equipment.RequestStatus = "Pending";
                Equipment.LastUpdated = DateTime.Now;

                await _db.SaveEquipmentAsync(Equipment);

                await _db.SendNotificationAsync(
                    "admin",
                    "📋 New Equipment Request",
                    $"{App.CurrentUser.FullName} requested '{Equipment.Name}'.");

                await Shell.Current.DisplayAlert("Success", $"Request for '{Equipment.Name}' submitted.", "OK");
                await Shell.Current.GoToAsync("..");
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