using Firetrack.Models;
using Firetrack.Services;
using Firetrack.Helpers;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class RequestEquipmentViewModel : ViewModelBase
    {
        private readonly DatabaseService? _db;
        private ObservableCollection<EquipmentModel> _availableEquipment = new();
        private bool _isBusy;

        public ObservableCollection<EquipmentModel> AvailableEquipment
        {
            get => _availableEquipment;
            set { _availableEquipment = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        // ✅ Nice-to-have for the XAML empty state
        public bool IsEmpty => !IsBusy && AvailableEquipment.Count == 0;

        public ICommand LoadAvailableCommand { get; }
        public ICommand ItemTappedCommand { get; }

        public RequestEquipmentViewModel()
        {
            _db = App.Database;

            // ✅ Command now wraps RefreshAsync so the page can call
            // either one with identical behaviour.
            LoadAvailableCommand = new Command(async () => await RefreshAsync());
            ItemTappedCommand = new Command<EquipmentModel>(OnItemTapped);

            // Initial load — fire and forget
            _ = RefreshAsync();
        }

        // ============================================================
        // REFRESH AVAILABLE LIST
        // ------------------------------------------------------------
        // Public so RequestEquipmentPage.OnAppearing can call it.
        //
        // WHY THIS MATTERS:
        //   Previously this method only ran in the constructor.
        //   Shell caches ShellContent pages, so navigating:
        //     Request page → tap item → request → ".." back
        //   returned to the SAME instance — constructor did NOT re-run
        //   and the just-requested item was still visible.
        //
        //   With OnAppearing → RefreshAsync(), the list updates every
        //   time the user lands on the page, so items with a Pending
        //   status correctly disappear.
        // ============================================================
        public async Task RefreshAsync()
        {
            if (_db == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    "⚠️ RequestEquipmentViewModel: Database is null — skipping refresh.");
                return;
            }

            IsBusy = true;
            try
            {
                // ✅ Run the query off the UI thread
                var all = await Task.Run(async () => await _db.GetEquipmentsAsync());

                // Available = not assigned, not requested, not damaged/disposed
                var available = all
                    .Where(e =>
                        e.Status == "Available" &&
                        string.IsNullOrEmpty(e.AssignedToUsername) &&
                        string.IsNullOrEmpty(e.RequestStatus))
                    .OrderBy(e => e.Category)
                    .ThenBy(e => e.Name)
                    .ToList();

                System.Diagnostics.Debug.WriteLine(
                    $"✅ RequestEquipmentViewModel: {available.Count} available item(s) of {all.Count} total.");

                // ✅ Mutate ObservableCollection on the UI thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    AvailableEquipment.Clear();
                    foreach (var item in available)
                        AvailableEquipment.Add(item);

                    OnPropertyChanged(nameof(IsEmpty));
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"❌ RequestEquipmentViewModel.RefreshAsync failed: {ex}");
                await Shell.Current.DisplayAlert(
                    "Error",
                    $"Could not load available equipment: {ex.Message}",
                    "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ============================================================
        // ITEM TAPPED → go to detail page
        // ============================================================
        private async void OnItemTapped(EquipmentModel equipment)
        {
            if (equipment == null) return;

            var navParams = new Dictionary<string, object>
            {
                { "equipment", equipment }
            };

            await Shell.Current.GoToAsync(Routes.EquipmentRequestDetail, navParams);
        }
    }
}