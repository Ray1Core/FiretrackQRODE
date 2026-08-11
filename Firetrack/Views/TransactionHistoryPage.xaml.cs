using Firetrack.Models;
using Firetrack.ViewModels;
using Microsoft.Maui.Controls;

namespace Firetrack.Views;

public partial class TransactionHistoryPage : ContentPage, IQueryAttributable
{
    private EquipmentModel? _passedEquipment;

    public TransactionHistoryPage()
    {
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey("equipment"))
        {
            _passedEquipment = query["equipment"] as EquipmentModel;
            BindingContext = new TransactionHistoryViewModel(_passedEquipment!);
        }
    }
}