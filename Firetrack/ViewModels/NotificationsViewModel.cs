using Firetrack.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class NotificationsViewModel : ViewModelBase
    {
        private ObservableCollection<NotificationModel> _notifications = new();

        public ObservableCollection<NotificationModel> Notifications
        {
            get => _notifications;
            set { _notifications = value; OnPropertyChanged(); }
        }

        // GoBackCommand removed – navigation is handled by Shell

        public NotificationsViewModel()
        {
            LoadNotifications();
        }

        private async void LoadNotifications()
        {
            if (App.CurrentUser == null || App.Database == null) return;

            var all = await App.Database.GetNotificationsForUserAsync(App.CurrentUser.Username);
            Notifications.Clear();
            foreach (var n in all)
                Notifications.Add(n);

            // Mark all as read
            await App.Database.MarkAllNotificationsAsReadAsync(App.CurrentUser.Username);
        }
    }
}