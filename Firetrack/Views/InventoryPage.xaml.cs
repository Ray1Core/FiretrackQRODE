using Firetrack.ViewModels;

namespace Firetrack.Views;

public partial class InventoryPage : ContentPage
{
    public InventoryPage()
    {
        InitializeComponent();
        BindingContext = new InventoryViewModel();
    }
}