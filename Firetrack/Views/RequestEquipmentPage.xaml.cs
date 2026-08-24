using Firetrack.ViewModels;
using Firetrack.Helpers;                // <-- Added
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
        // ✅ Replaced with Routes.EquipmentCategory
        await Shell.Current.GoToAsync(Routes.EquipmentCategory);
    }
}