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
        private string _selectedStatusFilter = "All";

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

        public string SelectedStatusFilter
        {
            get => _selectedStatusFilter;
            set
            {
                if (_selectedStatusFilter != value)
                {
                    _selectedStatusFilter = value;
                    OnPropertyChanged();
                    ApplyFilter();
                }
            }
        }

        public ObservableCollection<string> StatusFilterOptions { get; } = new()
        {
            "All",
            "Available",
            "Issued",
            "Damaged",
            "InRepair",
            "Disposed"
        };

        public ICommand LoadCategoriesCommand { get; }
        public ICommand ApplyFilterCommand { get; }
        public ICommand CategoryTappedCommand { get; }
        public ICommand GoToAddEquipmentCommand { get; }

        public EquipmentCategoryViewModel()
        {
            _db = App.Database!;
            IsAdmin = App.CurrentUser?.Role == "Admin";

            LoadCategoriesCommand = new Command(OnLoadCategories);
            ApplyFilterCommand = new Command(ApplyFilter);
            CategoryTappedCommand = new Command<CategoryGroup>(OnCategoryTapped);
            GoToAddEquipmentCommand = new Command(async () => await Shell.Current.GoToAsync("AddEquipmentPage"));

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

                // Apply status filter
                if (SelectedStatusFilter != "All")
                {
                    all = all.Where(e => e.Status == SelectedStatusFilter).ToList();
                }

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var search = SearchText.Trim();
                    all = all.Where(e =>
                        e.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        e.QRCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        e.Type.Contains(search, StringComparison.OrdinalIgnoreCase)
                    ).ToList();
                }

                // Group by Name + Type
                var grouped = all
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

        private void ApplyFilter()
        {
            _ = LoadCategoriesAsync();
        }

        private async void OnCategoryTapped(CategoryGroup category)
        {
            if (category == null) return;
            var navParams = new Dictionary<string, object> { { "categoryName", category.Name } };
            await Shell.Current.GoToAsync("CategoryItemsPage", navParams);
        }
    }
}