using Firetrack.ViewModels;

namespace Firetrack.Views;

public partial class ScannerPage : ContentPage
{
    public ScannerPage()
    {
        InitializeComponent();
        BindingContext = new ScannerViewModel();
    }
}