using Firetrack.Models;
using Firetrack.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class ClearanceViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private ObservableCollection<UserModel> _officers = new();
        private UserModel? _selectedOfficer;
        private ObservableCollection<EquipmentModel> _assignedEquipment = new();
        private EquipmentModel? _selectedEquipment;
        private string _statusMessage = string.Empty;
        private bool _isBusy;

        public ObservableCollection<UserModel> Officers
        {
            get => _officers;
            set { _officers = value; OnPropertyChanged(); }
        }

        public UserModel? SelectedOfficer
        {
            get => _selectedOfficer;
            set
            {
                _selectedOfficer = value;
                OnPropertyChanged();
                LoadAssignedEquipment();
            }
        }

        public ObservableCollection<EquipmentModel> AssignedEquipment
        {
            get => _assignedEquipment;
            set { _assignedEquipment = value; OnPropertyChanged(); }
        }

        public EquipmentModel? SelectedEquipment
        {
            get => _selectedEquipment;
            set { _selectedEquipment = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand MarkReturnedCommand { get; }
        public ICommand RefreshCommand { get; }

        public ClearanceViewModel()
        {
            _db = App.Database!;
            MarkReturnedCommand = new Command(OnMarkReturned);
            RefreshCommand = new Command(OnRefresh);
            LoadOfficers();
        }

        private async void LoadOfficers()
        {
            var users = await _db.GetUsersAsync();
            Officers.Clear();
            foreach (var u in users)
                Officers.Add(u);
        }

        private async void LoadAssignedEquipment()
        {
            if (SelectedOfficer == null)
            {
                AssignedEquipment.Clear();
                return;
            }

            IsBusy = true;
            var equipment = await _db.GetEquipmentsAssignedToUserAsync(SelectedOfficer.Username);
            AssignedEquipment.Clear();
            foreach (var eq in equipment)
                AssignedEquipment.Add(eq);
            IsBusy = false;
        }

        private async void OnMarkReturned()
        {
            if (SelectedEquipment == null)
            {
                StatusMessage = "Please select an equipment to mark as returned.";
                return;
            }

            if (SelectedEquipment.Status == "Available" && string.IsNullOrEmpty(SelectedEquipment.AssignedToUsername))
            {
                StatusMessage = "This equipment is already marked as returned.";
                return;
            }

            IsBusy = true;
            StatusMessage = string.Empty;

            try
            {
                var transaction = new TransactionModel
                {
                    EquipmentQR = SelectedEquipment.QRCode,
                    FromUser = SelectedEquipment.AssignedToUsername ?? "unknown",
                    ToUser = App.CurrentUser?.Username ?? "admin",
                    Timestamp = DateTime.Now,
                    Action = "Return",
                    Remarks = $"Returned by {SelectedEquipment.AssignedToUsername} during clearance."
                };

                SelectedEquipment.AssignedToUsername = null;
                SelectedEquipment.Status = "Available";
                SelectedEquipment.LastUpdated = DateTime.Now;

                await _db.SaveTransactionAsync(transaction);
                await _db.SaveEquipmentAsync(SelectedEquipment);

                // ✅ Log the action
                if (App.CurrentUser != null)
                {
                    await _db.LogActionAsync(
                        App.CurrentUser.Username,
                        "Clearance Return",
                        $"Marked '{SelectedEquipment.Name}' as returned from {SelectedEquipment.AssignedToUsername ?? "unknown"}");
                }

                StatusMessage = $"✅ {SelectedEquipment.Name} marked as returned.";
                LoadAssignedEquipment();
                SelectedEquipment = null;
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OnRefresh()
        {
            LoadOfficers();
            if (SelectedOfficer != null)
                LoadAssignedEquipment();
        }
    }
}