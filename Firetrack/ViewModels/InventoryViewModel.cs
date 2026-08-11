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
        private EquipmentModel? _selectedEquipment;
        private string _searchText = string.Empty;
        private bool _isBusy;

        public ObservableCollection<EquipmentModel> Equipments
        {
            get => _equipments;
            set { _equipments = value; OnPropertyChanged(); }
        }

        public EquipmentModel? SelectedEquipment
        {
            get => _selectedEquipment;
            set { _selectedEquipment = value; OnPropertyChanged(); }
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
        public ICommand DeleteEquipmentCommand { get; }
        public ICommand EditEquipmentCommand { get; }
        public ICommand ViewHistoryCommand { get; }
        public ICommand ShowEquipmentDetailsCommand { get; }
        public ICommand GoToAddEquipmentCommand { get; }

        public InventoryViewModel()
        {
            LoadEquipmentsCommand = new Command(OnLoadEquipments);
            SearchCommand = new Command(OnSearch);
            DeleteEquipmentCommand = new Command<EquipmentModel>(OnDeleteEquipment);
            EditEquipmentCommand = new Command<EquipmentModel>(OnEditEquipment);
            ViewHistoryCommand = new Command<EquipmentModel>(OnViewHistory);
            ShowEquipmentDetailsCommand = new Command<EquipmentModel>(OnShowEquipmentDetails);
            GoToAddEquipmentCommand = new Command(async () => await Shell.Current.GoToAsync("//AddEquipmentPage"));

            OnLoadEquipments();
        }

        private async void OnLoadEquipments()
        {
            await LoadEquipmentsAsync();
        }

        private async Task LoadEquipmentsAsync()
        {
            if (App.Database == null) return;

            IsBusy = true;
            try
            {
                var all = await App.Database.GetEquipmentsAsync();
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

        private async void OnDeleteEquipment(EquipmentModel? equipment)
        {
            if (equipment == null) return;

            bool confirm = await Shell.Current.DisplayAlert(
                "Confirm Delete",
                $"Are you sure you want to delete '{equipment.Name}'?",
                "Yes",
                "Cancel");

            if (!confirm) return;

            try
            {
                await App.Database!.DeleteEquipmentAsync(equipment);
                await Shell.Current.DisplayAlert("Success", $"'{equipment.Name}' deleted successfully.", "OK");
                await LoadEquipmentsAsync();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnEditEquipment(EquipmentModel? equipment)
        {
            if (equipment == null) return;

            string newName = await Shell.Current.DisplayPromptAsync(
                "Edit Equipment",
                $"Current name: {equipment.Name}\nEnter new name:",
                "Save",
                "Cancel",
                placeholder: equipment.Name);

            if (newName == null) return;

            if (!string.IsNullOrWhiteSpace(newName) && newName != equipment.Name)
            {
                equipment.Name = newName.Trim();
            }

            string newStatus = await Shell.Current.DisplayActionSheet(
                "Select Status",
                "Cancel",
                null,
                "Available",
                "Issued",
                "Damaged",
                "InRepair");

            if (!string.IsNullOrEmpty(newStatus) && newStatus != "Cancel")
            {
                equipment.Status = newStatus;
            }

            try
            {
                await App.Database!.SaveEquipmentAsync(equipment);
                await Shell.Current.DisplayAlert("Success", "Equipment updated.", "OK");
                await LoadEquipmentsAsync();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnViewHistory(EquipmentModel? equipment)
        {
            if (equipment == null) return;

            var navParams = new Dictionary<string, object>
            {
                { "equipment", equipment }
            };
            await Shell.Current.GoToAsync("//TransactionHistoryPage", navParams);
        }

        private async void OnShowEquipmentDetails(EquipmentModel? equipment)
        {
            if (equipment == null) return;

            await Shell.Current.DisplayAlert(
                "Equipment Details",
                $"Name: {equipment.Name}\n" +
                $"QR: {equipment.QRCode}\n" +
                $"Type: {equipment.Type}\n" +
                $"Status: {equipment.Status}\n" +
                $"Assigned to: {equipment.AssignedToUsername ?? "None"}\n" +
                $"Remarks: {equipment.Remarks ?? "None"}",
                "OK");
        }
    }
}