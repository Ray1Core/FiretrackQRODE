using Firetrack.Models;
using Firetrack.ViewModels;
using Microsoft.Maui.Controls;

namespace Firetrack.Views;

public partial class ReportDamagePage : ContentPage, IQueryAttributable
{
    public ReportDamagePage()
    {
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("equipment", out var eqObj) && eqObj is EquipmentModel equipment)
        {
            // Create a fresh ViewModel for this equipment
            BindingContext = new ReportDamageViewModel(equipment);
        }
        else
        {
            // If no equipment is passed, show error and go back
            DisplayAlert("Error", "Equipment not found.", "OK");
            _ = Shell.Current.GoToAsync("..");
        }
    }
}