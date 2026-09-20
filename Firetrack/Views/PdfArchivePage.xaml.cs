using Firetrack.ViewModels;
using Microsoft.Maui.Controls;

namespace Firetrack.Views;

public partial class PdfArchivePage : ContentPage
{
    public PdfArchivePage()
    {
        InitializeComponent();
        BindingContext = new PdfArchiveViewModel();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is PdfArchiveViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}