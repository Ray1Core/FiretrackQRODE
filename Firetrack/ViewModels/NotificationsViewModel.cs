using Firetrack.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class NotificationsViewModel : ViewModelBase
    {
        private ObservableCollection<NotificationModel> _notifications = new();
        private bool _isBusy;

        public ObservableCollection<NotificationModel> Notifications
        {
            get => _notifications;
            set { _notifications = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEmpty)); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        // ✅ NEW: shows a nice "no notifications" hint in the UI
        public bool IsEmpty => Notifications.Count == 0;

        public ICommand RefreshCommand { get; }

        public NotificationsViewModel()
        {
            RefreshCommand = new Command(async () => await LoadNotificationsAsync());
            _ = LoadNotificationsAsync();
        }

        private async Task LoadNotificationsAsync()
        {
            try
            {
                if (App.CurrentUser == null || App.Database == null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "⚠️ NotificationsViewModel: user or DB is null — skipping load.");
                    return;
                }

                IsBusy = true;

                System.Diagnostics.Debug.WriteLine(
                    $"🔔 Loading notifications for {App.CurrentUser.Username}...");

                var all = await App.Database.GetNotificationsForUserAsync(App.CurrentUser.Username);

                Notifications.Clear();
                foreach (var n in all) Notifications.Add(n);

                // ✅ FIX: Don't auto-mark as read. Let the badge on the Dashboard
                // reflect unread count. User taps individual items to mark them.
                // If you want "mark all read on view" later, add a button.
                System.Diagnostics.Debug.WriteLine(
                    $"🔔 Loaded {Notifications.Count} notification(s).");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ NotificationsViewModel load failed: {ex}");
                await Shell.Current.DisplayAlert("Error",
                    $"Could not load notifications: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}