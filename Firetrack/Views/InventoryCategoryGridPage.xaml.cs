using Firetrack.ViewModels;
using Microsoft.Maui.Controls;

namespace Firetrack.Views;

public partial class InventoryCategoryGridPage : ContentPage, IQueryAttributable
{
    private InventoryCategoryGridViewModel _viewModel;

    public InventoryCategoryGridPage()
    {
        InitializeComponent();
        _viewModel = new InventoryCategoryGridViewModel("inventory");
        BindingContext = _viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("mode", out var modeObj) && modeObj is string mode)
        {
            _viewModel.Mode = mode;
            _viewModel.LoadCategoriesCommand.Execute(null);
        }
    }
}