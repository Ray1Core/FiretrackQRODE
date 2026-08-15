using Firetrack.ViewModels;

namespace Firetrack.Views;

public partial class NotificationsPage : ContentPage
{
    public NotificationsPage()
    {
        InitializeComponent();
        BindingContext = new NotificationsViewModel();
    }
}