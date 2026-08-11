using Firetrack.ViewModels;

namespace Firetrack.Views;

public partial class ForgotPasswordPage : ContentPage
{
    public ForgotPasswordPage()
    {
        InitializeComponent();
        BindingContext = new ForgotPasswordViewModel();
    }
}