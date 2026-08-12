using Firetrack.Models;
using Firetrack.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class PendingRequestsViewModel : ViewModelBase
    {
        private ObservableCollection<EquipmentModel> _requests = new();
        private bool _isBusy;

        public ObservableCollection<EquipmentModel> Requests
        {
            get => _requests;
            set { _requests = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand LoadRequestsCommand { get; }
        public ICommand ApproveCommand { get; }
        public ICommand RejectCommand { get; }

        public PendingRequestsViewModel()
        {
            LoadRequestsCommand = new Command(OnLoadRequests);
            ApproveCommand = new Command<EquipmentModel>(OnApprove);
            RejectCommand = new Command<EquipmentModel>(OnReject);
            OnLoadRequests();
        }

        private async void OnLoadRequests()
        {
            if (App.Database == null) return;

            IsBusy = true;
            try
            {
                var list = await App.Database.GetPendingRequestsAsync();
                Requests.Clear();
                foreach (var item in list)
                    Requests.Add(item);
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

        private async void OnApprove(EquipmentModel? equipment)
        {
            if (equipment == null) return;
            if (App.CurrentUser == null) return;

            bool confirm = await Shell.Current.DisplayAlert(
                "Approve Request",
                $"Approve '{equipment.Name}' for {equipment.RequestedByUsername}?",
                "Yes",
                "Cancel");

            if (!confirm) return;

            try
            {
                var db = App.Database;
                if (db == null) return;

                await db.ApproveRequestAsync(equipment.QRCode, App.CurrentUser);

                // ✅ Log the action
                await db.LogActionAsync(
                    App.CurrentUser.Username,
                    "Approve Request",
                    $"Approved request for '{equipment.Name}' by {equipment.RequestedByUsername}");

                await Shell.Current.DisplayAlert("Success", $"Request approved. Equipment issued.", "OK");
                OnLoadRequests(); // refresh
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnReject(EquipmentModel? equipment)
        {
            if (equipment == null) return;
            if (App.CurrentUser == null) return;

            bool confirm = await Shell.Current.DisplayAlert(
                "Reject Request",
                $"Reject '{equipment.Name}' for {equipment.RequestedByUsername}?",
                "Yes",
                "Cancel");

            if (!confirm) return;

            try
            {
                var db = App.Database;
                if (db == null) return;

                await db.RejectRequestAsync(equipment.QRCode, App.CurrentUser);

                // ✅ Log the action
                await db.LogActionAsync(
                    App.CurrentUser.Username,
                    "Reject Request",
                    $"Rejected request for '{equipment.Name}' by {equipment.RequestedByUsername}");

                await Shell.Current.DisplayAlert("Success", $"Request rejected.", "OK");
                OnLoadRequests(); // refresh
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }
    }
}