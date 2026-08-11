using Firetrack.ViewModels;

namespace Firetrack.Views;

public partial class ClearancePage : ContentPage
{
    public ClearancePage()
    {
        InitializeComponent();
        BindingContext = new ClearanceViewModel();
    }
}