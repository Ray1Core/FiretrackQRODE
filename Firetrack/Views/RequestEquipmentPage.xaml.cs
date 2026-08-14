using Firetrack.ViewModels;

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
        // Redirect to the new category grid (no mode needed – role filters automatically)
        await Shell.Current.GoToAsync("EquipmentCategoryPage");
    }
}