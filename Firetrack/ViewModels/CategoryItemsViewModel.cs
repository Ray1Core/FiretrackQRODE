using System.Collections.ObjectModel;
using System.Windows.Input;
using Firetrack.Models;
using Firetrack.Services;
using Firetrack.Views;          // ✅ Required for page type names
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class CategoryItemsViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private string _categoryName = string.Empty;
        private ObservableCollection<EquipmentModel> _items = new();
        private bool _isBusy;

        public string CategoryName
        {
            get => _categoryName;
            set { _categoryName = value; OnPropertyChanged(); }
        }

        public ObservableCollection<EquipmentModel> Items
        {
            get => _items;
            set { _items = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand LoadItemsCommand { get; }
        public ICommand ItemTappedCommand { get; }

        public CategoryItemsViewModel(string categoryName)
        {
            _db = App.Database!;
            CategoryName = categoryName;

            LoadItemsCommand = new Command(OnLoadItems);
            ItemTappedCommand = new Command<EquipmentModel>(OnItemTapped);

            OnLoadItems();
        }

        private async void OnLoadItems()
        {
            if (_db == null) return;

            IsBusy = true;
            try
            {
                var all = await _db.GetEquipmentsAsync();

                var filtered = all.Where(e => e.Name == CategoryName);

                // Personnel sees only available items (no pending requests)
                if (App.CurrentUser?.Role == "Personnel")
                    filtered = filtered.Where(e => e.Status == "Available" && string.IsNullOrEmpty(e.RequestStatus));

                Items.Clear();
                foreach (var item in filtered.OrderBy(e => e.QRCode))
                    Items.Add(item);
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

        private async void OnItemTapped(EquipmentModel item)
        {
            if (item == null) return;

            var navParams = new Dictionary<string, object> { { "equipment", item } };

            // ✅ Use relative navigation (nameof) to preserve back stack
            if (App.CurrentUser?.Role == "Personnel")
                await Shell.Current.GoToAsync(nameof(EquipmentRequestDetailPage), navParams);
            else
                await Shell.Current.GoToAsync(nameof(EquipmentDetailPage), navParams);
        }
    }
}