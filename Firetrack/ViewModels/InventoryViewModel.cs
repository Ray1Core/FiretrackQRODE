using Firetrack.Models;
using Firetrack.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class InventoryViewModel : ViewModelBase
    {
        private ObservableCollection<EquipmentModel> _equipments = new();
        private string _searchText = string.Empty;
        private bool _isBusy;

        public ObservableCollection<EquipmentModel> Equipments
        {
            get => _equipments;
            set { _equipments = value; OnPropertyChanged(); }
        }

        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand LoadEquipmentsCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand GoToAddEquipmentCommand { get; }
        public ICommand ItemTappedCommand { get; }

        public InventoryViewModel()
        {
            LoadEquipmentsCommand = new Command(OnLoadEquipments);
            SearchCommand = new Command(OnSearch);
            GoToAddEquipmentCommand = new Command(async () => await Shell.Current.GoToAsync("AddEquipmentPage"));
            ItemTappedCommand = new Command<EquipmentModel>(OnItemTapped);

            OnLoadEquipments();
        }

        private async void OnLoadEquipments()
        {
            await LoadEquipmentsAsync();
        }

        private async Task LoadEquipmentsAsync()
        {
            var db = App.Database;
            if (db == null) return;

            IsBusy = true;
            try
            {
                var all = await db.GetEquipmentsAsync();
                var filtered = string.IsNullOrWhiteSpace(SearchText)
                    ? all
                    : all.Where(e => e.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                                  || e.QRCode.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                                  || e.Type.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();

                Equipments.Clear();
                foreach (var item in filtered)
                    Equipments.Add(item);
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

        private void OnSearch()
        {
            _ = LoadEquipmentsAsync();
        }

        private async void OnItemTapped(EquipmentModel equipment)
        {
            if (equipment == null) return;
            var navParams = new Dictionary<string, object> { { "equipment", equipment } };
            await Shell.Current.GoToAsync("EquipmentDetailPage", navParams);
        }
    }
}