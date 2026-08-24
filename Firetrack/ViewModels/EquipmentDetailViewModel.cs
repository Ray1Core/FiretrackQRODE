using Firetrack.Models;
using Firetrack.Services;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;

namespace Firetrack.ViewModels
{
    public class EquipmentDetailViewModel : ViewModelBase
    {
        private readonly DatabaseService? _db;
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

        public ICommand EditCommand { get; }
        public ICommand HistoryCommand { get; }
        public ICommand DeleteCommand { get; }

        public EquipmentDetailViewModel(EquipmentModel equipment)
        {
            _db = App.Database;
            Equipment = equipment;

            EditCommand = new Command(OnEdit);
            HistoryCommand = new Command(OnHistory);
            DeleteCommand = new Command(OnDelete);
        }

        private async void OnEdit()
        {
            if (_db == null)
            {
                await Shell.Current.DisplayAlert("Error", "Database not available.", "OK");
                return;
            }

            string newName = await Shell.Current.DisplayPromptAsync(
                "Edit Equipment",
                $"Current name: {Equipment.Name}\nEnter new name:",
                "Save",
                "Cancel",
                placeholder: Equipment.Name);

            if (newName == null) return;

            if (!string.IsNullOrWhiteSpace(newName) && newName != Equipment.Name)
                Equipment.Name = newName.Trim();

            string newStatus = await Shell.Current.DisplayActionSheet(
                "Select Status",
                "Cancel",
                null,
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
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void OnHistory()
        {
            var navParams = new Dictionary<string, object> { { "equipment", Equipment } };
            await Shell.Current.GoToAsync("TransactionHistoryPage", navParams);
        }

        private async void OnDelete()
        {
            if (_db == null)
            {
                await Shell.Current.DisplayAlert("Error", "Database not available.", "OK");
                return;
            }

            bool confirm = await Shell.Current.DisplayAlert(
                "Confirm Delete",
                $"Delete '{Equipment.Name}'?",
                "Yes", "Cancel");

            if (!confirm) return;

            try
            {
                IsBusy = true;
                await _db.DeleteEquipmentAsync(Equipment);
                await Shell.Current.DisplayAlert("Success", "Equipment deleted.", "OK");
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