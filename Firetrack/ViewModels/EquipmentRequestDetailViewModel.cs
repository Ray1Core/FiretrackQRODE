using Firetrack.Models;
using Firetrack.Services;
using Firetrack.Helpers;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class EquipmentRequestDetailViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private EquipmentModel _equipment = null!;
        private bool _isBusy;

        public EquipmentModel Equipment
        {
            get => _equipment;
            set { _equipment = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand RequestCommand { get; }
        public ICommand RequestDisposalCommand { get; }

        public EquipmentRequestDetailViewModel(EquipmentModel equipment)
        {
            _db = App.Database!;
            Equipment = equipment;
            RequestCommand = new Command(OnRequest);
            RequestDisposalCommand = new Command(OnRequestDisposal);
        }

        // ============================================================
        // REQUEST EQUIPMENT
        // ------------------------------------------------------------
        // Personnel taps this to request an Available item.
        //
        // FIXES APPLIED:
        //  1. Notification was sent to "admin" (invalid username) and
        //     was silently dropped by SaveNotificationAsync.
        //     Now uses the seeded admin email "admin@firetrack.gov".
        //  2. Added audit-log entry so the action shows on AuditLogPage.
        // ============================================================
        private async void OnRequest()
        {
            if (App.CurrentUser == null)
            {
                await Shell.Current.DisplayAlert("Error", "You must be logged in.", "OK");
                return;
            }

            // Guard against duplicate requests
            if (!string.IsNullOrEmpty(Equipment.RequestStatus))
            {
                await Shell.Current.DisplayAlert(
                    "Already Requested",
                    $"You already have a '{Equipment.RequestStatus}' request for this item.",
                    "OK");
                return;
            }

            bool confirm = await Shell.Current.DisplayAlert(
                "Confirm Request",
                $"Request '{Equipment.Name}'?",
                "Yes",
                "Cancel");

            if (!confirm) return;

            IsBusy = true;
            try
            {
                Equipment.RequestedByUsername = App.CurrentUser.Username;
                Equipment.RequestStatus = "Pending";
                Equipment.LastUpdated = DateTime.Now;

                await _db.SaveEquipmentAsync(Equipment);

                // ✅ FIX #1: was "admin" — that username doesn't exist.
                // The seeded admin's Username is actually their Email.
                await _db.SendNotificationAsync(
                    "admin@firetrack.gov",
                    "📋 New Equipment Request",
                    $"{App.CurrentUser.FullName} requested '{Equipment.Name}' ({Equipment.QRCode}).");

                // ✅ FIX #2: audit log was missing entirely for this action.
                await _db.LogActionAsync(
                    App.CurrentUser.Username,
                    "Request Equipment",
                    $"Requested '{Equipment.Name}' ({Equipment.QRCode})");

                await Shell.Current.DisplayAlert(
                    "Success",
                    $"Request for '{Equipment.Name}' submitted to Admin.",
                    "OK");

                await Shell.Current.GoToAsync("..");
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

        // ============================================================
        // REQUEST DISPOSAL
        // ------------------------------------------------------------
        // Only allowed when Equipment.Status == "Damaged".
        // DatabaseService.RequestDisposalAsync already notifies the
        // admin correctly (uses "admin@firetrack.gov"), but we add the
        // audit-log line here so the request is traceable.
        // ============================================================
        private async void OnRequestDisposal()
        {
            if (App.CurrentUser == null)
            {
                await Shell.Current.DisplayAlert("Error", "You must be logged in.", "OK");
                return;
            }

            // Only allow if equipment is currently damaged
            if (Equipment.Status != "Damaged")
            {
                await Shell.Current.DisplayAlert(
                    "Info",
                    "Only damaged equipment can be marked for disposal.",
                    "OK");
                return;
            }

            bool confirm = await Shell.Current.DisplayAlert(
                "Confirm Disposal Request",
                $"Request disposal of '{Equipment.Name}'?\nReason: This equipment is damaged.",
                "Yes",
                "Cancel");

            if (!confirm) return;

            string reason = await Shell.Current.DisplayPromptAsync(
                "Reason for Disposal",
                "Please provide more detail (optional):",
                "Submit",
                "Cancel");

            if (reason == null) return; // user cancelled

            IsBusy = true;
            try
            {
                bool success = await _db.RequestDisposalAsync(
                    Equipment.QRCode,
                    App.CurrentUser.Username,
                    string.IsNullOrWhiteSpace(reason) ? "Damaged equipment" : reason);

                if (success)
                {
                    // ✅ Audit log the disposal request too.
                    await _db.LogActionAsync(
                        App.CurrentUser.Username,
                        "Request Disposal",
                        $"Disposal requested for '{Equipment.Name}' ({Equipment.QRCode}). Reason: {reason}");

                    await Shell.Current.DisplayAlert(
                        "Success",
                        $"Disposal request for '{Equipment.Name}' submitted to Admin.",
                        "OK");

                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await Shell.Current.DisplayAlert(
                        "Error",
                        "Failed to submit disposal request.",
                        "OK");
                }
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
    }
}