using Firetrack.ViewModels;

namespace Firetrack.Views;

public partial class AddUserPage : ContentPage
{
    public AddUserPage()
    {
        InitializeComponent();
        BindingContext = new AddUserViewModel();
    }
}