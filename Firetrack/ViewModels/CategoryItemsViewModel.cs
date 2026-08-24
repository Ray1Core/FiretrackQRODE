using System.Collections.ObjectModel;
using System.Windows.Input;
using Firetrack.Models;
using Firetrack.Services;
using Firetrack.Views;
using Firetrack.Helpers;                // <-- Added
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class CategoryItemsViewModel : ViewModelBase
    {
        private readonly DatabaseService? _db; // nullable
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
            _db = App.Database;
            CategoryName = categoryName;

            LoadItemsCommand = new Command(OnLoadItems);
            ItemTappedCommand = new Command<EquipmentModel>(OnItemTapped);

            // Only load if database is available
            if (_db != null)
                OnLoadItems();
            else
            {
                // ✅ Use Application.Current.MainPage if Shell is not ready
                var page = Application.Current?.MainPage;
                if (page != null)
                    page.DisplayAlert("Error", "Database not available.", "OK");
                else
                    System.Diagnostics.Debug.WriteLine("⚠️ Database not available and Shell/MainPage is null.");
            }
        }

        private async void OnLoadItems()
        {
            if (_db == null)
            {
                await Shell.Current.DisplayAlert("Error", "Database not available.", "OK");
                return;
            }

            IsBusy = true;
            try
            {
                // Run heavy query on background thread
                var all = await Task.Run(async () => await _db.GetEquipmentsAsync());

                var filtered = all.Where(e => e.Name == CategoryName);

                if (App.CurrentUser?.Role == "Personnel")
                    filtered = filtered.Where(e => e.Status == "Available" && string.IsNullOrEmpty(e.RequestStatus));

                var itemsList = filtered.OrderBy(e => e.QRCode).ToList();

                // Update UI on main thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Items.Clear();
                    foreach (var item in itemsList)
                        Items.Add(item);
                });
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

            if (App.CurrentUser?.Role == "Personnel")
                await Shell.Current.GoToAsync(Routes.EquipmentRequestDetail, navParams);  // <-- Updated
            else
                await Shell.Current.GoToAsync(Routes.EquipmentDetail, navParams);         // <-- Updated
        }
    }
}