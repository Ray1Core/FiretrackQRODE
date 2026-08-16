using Firetrack.ViewModels;
using Microsoft.Maui.Controls;

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

        // ✅ Ensure flyout is enabled on dashboard (after login)
        Shell.SetFlyoutBehavior(this, FlyoutBehavior.Flyout);

        if (BindingContext is DashboardViewModel vm)
        {
            vm.RefreshDashboard();
        }
    }
}