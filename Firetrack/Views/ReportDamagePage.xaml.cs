using Firetrack.Models;
using Firetrack.ViewModels;
using Microsoft.Maui.Controls;

namespace Firetrack.Views;

public partial class ReportDamagePage : ContentPage, IQueryAttributable
{
    private EquipmentModel? _passedEquipment;

    public ReportDamagePage()
    {
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey("equipment"))
        {
            _passedEquipment = query["equipment"] as EquipmentModel;
            BindingContext = new ReportDamageViewModel(_passedEquipment!);
        }
    }
}