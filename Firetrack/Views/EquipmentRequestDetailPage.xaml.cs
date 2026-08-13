using Firetrack.Models;
using Firetrack.ViewModels;
using Microsoft.Maui.Controls;

namespace Firetrack.Views;

public partial class EquipmentRequestDetailPage : ContentPage, IQueryAttributable
{
    public EquipmentRequestDetailPage()
    {
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("equipment", out var eqObj) && eqObj is EquipmentModel equipment)
        {
            BindingContext = new EquipmentRequestDetailViewModel(equipment);
        }
        else
        {
            DisplayAlert("Error", "Equipment not found.", "OK");
        }
    }
}