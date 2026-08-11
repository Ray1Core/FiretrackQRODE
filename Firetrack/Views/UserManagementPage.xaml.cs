using Firetrack.ViewModels;

namespace Firetrack.Views;

public partial class UserManagementPage : ContentPage
{
    public UserManagementPage()
    {
        InitializeComponent();
        BindingContext = new UserManagementViewModel();
    }
}