using Firetrack.ViewModels;

namespace Firetrack.Views;

public partial class AddEquipmentPage : ContentPage
{
    public AddEquipmentPage()
    {
        InitializeComponent();
        BindingContext = new AddEquipmentViewModel();
    }
}