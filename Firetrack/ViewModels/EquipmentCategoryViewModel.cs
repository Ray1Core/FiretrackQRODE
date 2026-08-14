using System.Collections.ObjectModel;
using System.Windows.Input;
using Firetrack.Models;
using Firetrack.Services;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class EquipmentCategoryViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private ObservableCollection<CategoryGroup> _categories = new();
        private bool _isBusy;

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

        public EquipmentCategoryViewModel()
        {
            _db = App.Database!;

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

                // Filter based on user role
                var isPersonnel = App.CurrentUser?.Role == "Personnel";
                var filtered = isPersonnel
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
                { "categoryName", category.Name }
            };
            await Shell.Current.GoToAsync("CategoryItemsPage", navParams);
        }
    }
}