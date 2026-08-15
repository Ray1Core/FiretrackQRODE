using Firetrack.ViewModels;
using Microsoft.Maui.Controls;

namespace Firetrack.Views;

public partial class GenerateQRPage : ContentPage
{
    public GenerateQRPage()
    {
        InitializeComponent();
        BindingContext = new GenerateQRViewModel();
    }
}