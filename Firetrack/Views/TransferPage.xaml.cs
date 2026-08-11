using Firetrack.ViewModels;

namespace Firetrack.Views;

public partial class TransferPage : ContentPage
{
    public TransferPage()
    {
        InitializeComponent();
        BindingContext = new TransferViewModel();
    }
}