using Firetrack.ViewModels;

namespace Firetrack.Views;

public partial class PendingRequestsPage : ContentPage
{
    public PendingRequestsPage()
    {
        InitializeComponent();
        BindingContext = new PendingRequestsViewModel();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Rebuild the list every time the admin lands on this page.
        // Fixes the "request submitted after first visit doesn't show up"
        // bug caused by Shell caching the page instance.
        if (BindingContext is PendingRequestsViewModel vm)
            _ = vm.RefreshAsync();
    }
}