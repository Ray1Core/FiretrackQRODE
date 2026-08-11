using Firetrack.ViewModels;

namespace Firetrack.Views;

public partial class GenerateQRPage : ContentPage
{
    public GenerateQRPage()
    {
        InitializeComponent();
        BindingContext = new GenerateQRViewModel();
    }
}