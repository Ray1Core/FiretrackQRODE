using Firetrack.ViewModels;

namespace Firetrack.Views;

public partial class RequestEquipmentPage : ContentPage
{
    public RequestEquipmentPage()
    {
        InitializeComponent();
        // No BindingContext needed – we redirect immediately
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Shell.Current.GoToAsync("InventoryCategoryGridPage?mode=request");
    }
}