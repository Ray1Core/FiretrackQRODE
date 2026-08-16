using Firetrack.ViewModels;
using Microsoft.Maui.Controls;

namespace Firetrack.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
        BindingContext = new LoginViewModel();

        // ✅ Disable flyout entirely on login page (no swipe, no hamburger)
        Shell.SetFlyoutBehavior(this, FlyoutBehavior.Disabled);
    }
}