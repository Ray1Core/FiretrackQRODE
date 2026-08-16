using Firetrack.ViewModels;

namespace Firetrack.Views;

public partial class DualScanPage : ContentPage
{
    public DualScanPage()
    {
        InitializeComponent();
        BindingContext = new DualScanViewModel();
    }
}