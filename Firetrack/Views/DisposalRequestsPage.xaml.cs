using Firetrack.ViewModels;

namespace Firetrack.Views;

public partial class DisposalRequestsPage : ContentPage
{
    public DisposalRequestsPage()
    {
        InitializeComponent();
        BindingContext = new DisposalRequestsViewModel();
    }
}