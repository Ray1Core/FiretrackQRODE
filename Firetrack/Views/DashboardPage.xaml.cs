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

        Shell.SetFlyoutBehavior(this, FlyoutBehavior.Flyout);

        if (BindingContext is DashboardViewModel vm)
        {
            vm.RefreshDashboard();
            vm.IsAdmin = App.CurrentUser?.Role == "Admin";

            System.Diagnostics.Debug.WriteLine(
                $"🔍 Dashboard OnAppearing: IsAdmin = {vm.IsAdmin}, User = {App.CurrentUser?.Email}, Role = {App.CurrentUser?.Role}");
        }
    }
}