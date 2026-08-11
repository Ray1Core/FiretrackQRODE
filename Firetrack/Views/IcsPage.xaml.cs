using Firetrack.Models;
using Firetrack.ViewModels;
using Microsoft.Maui.Controls;

namespace Firetrack.Views;

public partial class IcsPage : ContentPage, IQueryAttributable
{
    public IcsPage()
    {
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        // Try to retrieve both objects in one condition
        if (query.TryGetValue("equipment", out var eqObj) && eqObj is EquipmentModel equipment &&
            query.TryGetValue("officer", out var offObj) && offObj is UserModel officer)
        {
            System.Diagnostics.Debug.WriteLine($"✅ ICS Page: Equipment={equipment.Name}, Officer={officer.FullName}");
            BindingContext = new IcsViewModel(equipment, officer);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("❌ ICS Page: Missing data, using fallback.");
            BindingContext = new IcsViewModel(null, null);
        }
    }
}