using Firetrack.ViewModels;

namespace Firetrack.Views;

public partial class RequestEquipmentPage : ContentPage
{
    public RequestEquipmentPage()
    {
        InitializeComponent();
        BindingContext = new RequestEquipmentViewModel();
    }
}