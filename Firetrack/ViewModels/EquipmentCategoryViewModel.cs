using Firetrack.Models;
using Firetrack.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class EquipmentCategoryViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private ObservableCollection<CategoryGroup> _categories = new();
        private bool _isBusy;
        private bool _isAdmin;
        private string _searchText = string.Empty;

        // ---- Simplified Metrics (only 3) ----
        private int _availableCount;
        private int _stackInCount;    // Issued
        private int _stackOutCount;   // Disposed

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

        // ---- Metric properties ----
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
            _db = App.Database!;
            IsAdmin = App.CurrentUser?.Role == "Admin";

            LoadCategoriesCommand = new Command(OnLoadCategories);
            SearchCommand = new Command(OnSearch);
            CategoryTappedCommand = new Command<CategoryGroup>(OnCategoryTapped);
            GoToAddEquipmentCommand = new Command(async () => await Shell.Current.GoToAsync("//AddEquipmentPage"));

            OnLoadCategories();
        }

        private async void OnLoadCategories()
        {
            await LoadCategoriesAsync();
        }

        private async Task LoadCategoriesAsync()
        {
            if (_db == null) return;

            IsBusy = true;
            try
            {
                var all = await _db.GetEquipmentsAsync();

                // ---- Update metrics (only three) ----
                AvailableCount = all.Count(e => e.Status == "Available");
                StackInCount = all.Count(e => e.Status == "Issued");
                StackOutCount = all.Count(e => e.Status == "Disposed");

                // ---- Apply search filter ----
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

                // ---- Group by Name + Type ----
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

                Categories.Clear();
                foreach (var item in grouped)
                    Categories.Add(item);
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
            await Shell.Current.GoToAsync("//CategoryItemsPage", navParams);
        }
    }
}