using System.Collections.ObjectModel;
using System.Windows.Input;
using Firetrack.Models;
using Firetrack.Services;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class InventoryCategoryGridViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private string _mode = "inventory";
        private ObservableCollection<CategoryGroup> _categories = new();
        private bool _isBusy;

        public string Mode
        {
            get => _mode;
            set { _mode = value; OnPropertyChanged(); }
        }

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

        public ICommand LoadCategoriesCommand { get; }
        public ICommand CategoryTappedCommand { get; }
        public ICommand GoToAddEquipmentCommand { get; }

        public InventoryCategoryGridViewModel(string mode = "inventory")
        {
            _db = App.Database!;
            Mode = mode;

            LoadCategoriesCommand = new Command(OnLoadCategories);
            CategoryTappedCommand = new Command<CategoryGroup>(OnCategoryTapped);
            GoToAddEquipmentCommand = new Command(async () => await Shell.Current.GoToAsync("AddEquipmentPage"));

            OnLoadCategories();
        }

        private async void OnLoadCategories()
        {
            if (_db == null) return;

            IsBusy = true;
            try
            {
                var all = await _db.GetEquipmentsAsync();

                var filtered = Mode == "request"
                    ? all.Where(e => e.Status == "Available" && string.IsNullOrEmpty(e.RequestStatus))
                    : all;

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

        private async void OnCategoryTapped(CategoryGroup category)
        {
            if (category == null) return;

            var navParams = new Dictionary<string, object>
            {
                { "categoryName", category.Name },
                { "mode", Mode }
            };
            await Shell.Current.GoToAsync("CategoryItemsPage", navParams);
        }
    }
}