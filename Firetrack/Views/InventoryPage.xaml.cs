using Firetrack.ViewModels;

namespace Firetrack.Views;

public partial class InventoryPage : ContentPage
{
    public InventoryPage()
    {
        InitializeComponent();
        // No BindingContext needed – we redirect immediately
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Shell.Current.GoToAsync("InventoryCategoryGridPage?mode=inventory");
    }
}