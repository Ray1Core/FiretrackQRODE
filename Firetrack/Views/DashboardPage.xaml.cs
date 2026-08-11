using Firetrack.ViewModels;

namespace Firetrack.Views;

public partial class DashboardPage : ContentPage
{
    public DashboardPage()
    {
        InitializeComponent();
        BindingContext = new DashboardViewModel();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Refresh the ViewModel when the page appears
        if (BindingContext is DashboardViewModel vm)
        {
            vm.RefreshDashboard();
        }
    }
}