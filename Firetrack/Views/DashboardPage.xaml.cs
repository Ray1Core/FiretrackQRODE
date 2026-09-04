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
            // Refresh all data – this already updates IsAdmin
            vm.RefreshDashboard();

            // 🔒 Extra safety: explicitly set IsAdmin based on current user
            // (RefreshDashboard already does this, but this is an extra check)
            vm.IsAdmin = App.CurrentUser?.Role == "Admin";
            // No need to call OnPropertyChanged – the setter does it

            // Debug output to verify
            System.Diagnostics.Debug.WriteLine($"🔍 Dashboard OnAppearing: IsAdmin = {vm.IsAdmin}, User = {App.CurrentUser?.Email}, Role = {App.CurrentUser?.Role}");
        }
    }
}