using Firetrack.Models;
using Firetrack.Services;
using Firetrack.Helpers;                // <-- Added
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class RequestEquipmentViewModel : ViewModelBase
    {
        private ObservableCollection<EquipmentModel> _availableEquipment = new();
        private bool _isBusy;

        public ObservableCollection<EquipmentModel> AvailableEquipment
        {
            get => _availableEquipment;
            set { _availableEquipment = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand LoadAvailableCommand { get; }
        public ICommand ItemTappedCommand { get; }

        public RequestEquipmentViewModel()
        {
            LoadAvailableCommand = new Command(async () => await OnLoadAvailable());
            ItemTappedCommand = new Command<EquipmentModel>(OnItemTapped);
            _ = OnLoadAvailable();
        }

        private async Task OnLoadAvailable()
        {
            if (App.Database == null) return;

            IsBusy = true;
            try
            {
                var all = await App.Database.GetEquipmentsAsync();
                var available = all.Where(e =>
                    e.Status == "Available" &&
                    string.IsNullOrEmpty(e.AssignedToUsername) &&
                    string.IsNullOrEmpty(e.RequestStatus));
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

        private async void OnItemTapped(EquipmentModel equipment)
        {
            if (equipment == null) return;
            var navParams = new Dictionary<string, object> { { "equipment", equipment } };
            // ✅ Replaced with Routes.EquipmentRequestDetail
            await Shell.Current.GoToAsync(Routes.EquipmentRequestDetail, navParams);
        }
    }
}