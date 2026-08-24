using Firetrack.Models;
using Firetrack.Services;
using Firetrack.Helpers;                // <-- Added
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;

namespace Firetrack.ViewModels
{
    public class EquipmentCategoryViewModel : ViewModelBase
    {
        private readonly DatabaseService? _db;
        private ObservableCollection<CategoryGroup> _categories = new();
        private bool _isBusy;
        private bool _isAdmin;
        private string _searchText = string.Empty;
        private int _availableCount;
        private int _stackInCount;
        private int _stackOutCount;

        public ObservableCollection<CategoryGroup> Categories
        {
            get => _categories;
            set { _categories = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public bool IsAdmin
        {
            get => _isAdmin;
            set { _isAdmin = value; OnPropertyChanged(); }
        }

        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); }
        }

        public int AvailableCount
        {
            get => _availableCount;
            set { _availableCount = value; OnPropertyChanged(); }
        }

        public int StackInCount
        {
            get => _stackInCount;
            set { _stackInCount = value; OnPropertyChanged(); }
        }

        public int StackOutCount
        {
            get => _stackOutCount;
            set { _stackOutCount = value; OnPropertyChanged(); }
        }

        public ICommand LoadCategoriesCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand CategoryTappedCommand { get; }
        public ICommand GoToAddEquipmentCommand { get; }

        public EquipmentCategoryViewModel()
        {
            _db = App.Database;
            IsAdmin = App.CurrentUser?.Role == "Admin";

            LoadCategoriesCommand = new Command(OnLoadCategories);
            SearchCommand = new Command(OnSearch);
            CategoryTappedCommand = new Command<CategoryGroup>(OnCategoryTapped);
            // ✅ Replaced with Routes.AddEquipment
            GoToAddEquipmentCommand = new Command(async () =>
            {
                if (_db == null)
                {
                    await Shell.Current.DisplayAlert("Error", "Database not available.", "OK");
                    return;
                }
                await Shell.Current.GoToAsync(Routes.AddEquipment);
            });

            if (_db != null)
                OnLoadCategories();
            else
                Shell.Current.DisplayAlert("Error", "Database not available.", "OK");
        }

        private async void OnLoadCategories()
        {
            await LoadCategoriesAsync();
        }

        private async Task LoadCategoriesAsync()
        {
            if (_db == null)
            {
                await Shell.Current.DisplayAlert("Error", "Database not available.", "OK");
                return;
            }

            IsBusy = true;
            try
            {
                var all = await Task.Run(async () => await _db.GetEquipmentsAsync());

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AvailableCount = all.Count(e => e.Status == "Available");
                    StackInCount = all.Count(e => e.Status == "Issued");
                    StackOutCount = all.Count(e => e.Status == "Disposed");
                });

                var filtered = all;
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var search = SearchText.Trim();
                    filtered = filtered.Where(e =>
                        e.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        e.QRCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        e.Type.Contains(search, StringComparison.OrdinalIgnoreCase)
                    ).ToList();
                }

                var grouped = filtered
                    .GroupBy(e => new { e.Name, e.Type })
                    .Select(g => new CategoryGroup
                    {
                        Name = g.Key.Name,
                        Type = g.Key.Type,
                        Count = g.Count()
                    })
                    .OrderBy(c => c.Type)
                    .ThenBy(c => c.Name)
                    .ToList();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Categories.Clear();
                    foreach (var item in grouped)
                        Categories.Add(item);
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

        private void OnSearch()
        {
            _ = LoadCategoriesAsync();
        }

        private async void OnCategoryTapped(CategoryGroup category)
        {
            if (category == null) return;
            var navParams = new Dictionary<string, object> { { "categoryName", category.Name } };
            // ✅ Replaced with Routes.CategoryItems
            await Shell.Current.GoToAsync(Routes.CategoryItems, navParams);
        }
    }
}