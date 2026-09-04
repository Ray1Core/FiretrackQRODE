using Firetrack.ViewModels;
using Firetrack.Helpers;
using Microsoft.Maui.Controls;

namespace Firetrack.Views;

public partial class RequestEquipmentPage : ContentPage
{
    public RequestEquipmentPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Redirect to the role‑appropriate Equipment Category page
        await Shell.Current.GoToAsync(Routes.GetEquipmentCategoryRoute());
    }
}