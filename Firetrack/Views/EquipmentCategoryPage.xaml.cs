using Firetrack.ViewModels;
using Microsoft.Maui.Controls;

namespace Firetrack.Views;

public partial class EquipmentCategoryPage : ContentPage
{
    public EquipmentCategoryPage()
    {
        InitializeComponent();
        BindingContext = new EquipmentCategoryViewModel();
    }
}