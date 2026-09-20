using Firetrack.Models;
using Firetrack.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class PendingRequestsViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private ObservableCollection<EquipmentModel> _requests = new();
        private bool _isBusy;

        public ObservableCollection<EquipmentModel> Requests
        {
            get => _requests;
            set
            {
                _requests = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEmpty));
            }
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

        // ✅ New — lets the XAML show a proper empty state
        public bool IsEmpty => !IsBusy && Requests.Count == 0;

        public ICommand LoadRequestsCommand { get; }
        public ICommand ApproveCommand { get; }
        public ICommand RejectCommand { get; }

        public PendingRequestsViewModel()
        {
            _db = App.Database!;
            LoadRequestsCommand = new Command(async () => await RefreshAsync());
            ApproveCommand = new Command<EquipmentModel>(OnApprove);
            RejectCommand = new Command<EquipmentModel>(OnReject);

            // Initial load — fire and forget
            _ = RefreshAsync();
        }

        // ============================================================
        // REFRESH PENDING LIST
        // ------------------------------------------------------------
        // Public so PendingRequestsPage.OnAppearing can call it.
        //
        // WHY THIS MATTERS:
        //   Shell caches ShellContent pages, so navigating away and
        //   back to this page re-uses the SAME instance. Previously
        //   OnLoadRequests only ran in the constructor, so a request
        //   submitted by Personnel *after* the admin's first visit
        //   did NOT appear until the app was restarted.
        //
        //   With OnAppearing → RefreshAsync(), the list is rebuilt
        //   every time the admin lands on this page.
        // ============================================================
        public async Task RefreshAsync()
        {
            if (_db == null) return;

            IsBusy = true;

            try
            {
                var list = await Task.Run(async () => await _db.GetPendingRequestsAsync());

                System.Diagnostics.Debug.WriteLine(
                    $"📋 PendingRequestsViewModel: {list.Count} pending request(s).");

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Requests.Clear();
                    foreach (var item in list)
                        Requests.Add(item);

                    OnPropertyChanged(nameof(IsEmpty));
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"❌ PendingRequestsViewModel.RefreshAsync failed: {ex}");
                await Shell.Current.DisplayAlert(
                    "Error",
                    $"Could not load pending requests: {ex.Message}",
                    "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ============================================================
        // APPROVE REQUEST
        // ------------------------------------------------------------
        // DatabaseService.ApproveRequestAsync handles:
        //   • Closing stale 'Assigned' rows
        //   • Inserting a fresh 'Assigned' row
        //   • Setting equipment Status = "Issued", AssignedToUsername
        //   • Sending the personnel notification
        //
        // This VM adds the admin-side audit log entry on top.
        // Split try/catch keeps the DB write as the "critical path" —
        // a log failure won't roll back a successful approve.
        // ============================================================
        private async void OnApprove(EquipmentModel? equipment)
        {
            if (equipment == null) return;

            // ✅ Prevent double-tap while the approve is in flight
            if (IsBusy) return;

            if (App.CurrentUser == null)
            {
                await Shell.Current.DisplayAlert(
                    "Error",
                    "You must be logged in to approve requests.",
                    "OK");
                return;
            }

            bool confirm = await Shell.Current.DisplayAlert(
                "Approve Request",
                $"Approve '{equipment.Name}' ({equipment.QRCode}) for " +
                $"{equipment.RequestedByUsername}?",
                "Yes",
                "Cancel");

            if (!confirm) return;

            IsBusy = true;

            try
            {
                // ---------- 1. CRITICAL PATH: approve in DB ----------
                await _db.ApproveRequestAsync(equipment.QRCode, App.CurrentUser);

                System.Diagnostics.Debug.WriteLine(
                    $"✅ Approved '{equipment.Name}' for {equipment.RequestedByUsername}");

                // ---------- 2. BEST-EFFORT: audit log ----------
                try
                {
                    await _db.LogActionAsync(
                        App.CurrentUser.Username,
                        "Approve Request",
                        $"Approved request for '{equipment.Name}' ({equipment.QRCode}) " +
                        $"by {equipment.RequestedByUsername}");
                }
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"⚠️ Audit log failed (approve still succeeded): {logEx.Message}");
                }

                await Shell.Current.DisplayAlert(
                    "Success",
                    "Request approved. Equipment issued.",
                    "OK");

                await RefreshAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Approve failed: {ex}");
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ============================================================
        // REJECT REQUEST
        // ------------------------------------------------------------
        // DatabaseService.RejectRequestAsync handles:
        //   • Clearing RequestedByUsername / RequestStatus on Equipment
        //   • Sending the personnel notification
        //
        // This VM adds the admin-side audit log entry.
        // ============================================================
        private async void OnReject(EquipmentModel? equipment)
        {
            if (equipment == null) return;

            // ✅ Prevent double-tap while the reject is in flight
            if (IsBusy) return;

            if (App.CurrentUser == null)
            {
                await Shell.Current.DisplayAlert(
                    "Error",
                    "You must be logged in to reject requests.",
                    "OK");
                return;
            }

            bool confirm = await Shell.Current.DisplayAlert(
                "Reject Request",
                $"Reject '{equipment.Name}' ({equipment.QRCode}) for " +
                $"{equipment.RequestedByUsername}?",
                "Yes",
                "Cancel");

            if (!confirm) return;

            IsBusy = true;

            try
            {
                // ---------- 1. CRITICAL PATH: reject in DB ----------
                await _db.RejectRequestAsync(equipment.QRCode, App.CurrentUser);

                System.Diagnostics.Debug.WriteLine(
                    $"✅ Rejected '{equipment.Name}' for {equipment.RequestedByUsername}");

                // ---------- 2. BEST-EFFORT: audit log ----------
                try
                {
                    await _db.LogActionAsync(
                        App.CurrentUser.Username,
                        "Reject Request",
                        $"Rejected request for '{equipment.Name}' ({equipment.QRCode}) " +
                        $"by {equipment.RequestedByUsername}");
                }
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"⚠️ Audit log failed (reject still succeeded): {logEx.Message}");
                }

                await Shell.Current.DisplayAlert(
                    "Success",
                    "Request rejected.",
                    "OK");

                await RefreshAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Reject failed: {ex}");
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}