using Firetrack.ViewModels;

namespace Firetrack.Views;

public partial class PendingRequestsPage : ContentPage
{
    public PendingRequestsPage()
    {
        InitializeComponent();
        BindingContext = new PendingRequestsViewModel();
    }
}