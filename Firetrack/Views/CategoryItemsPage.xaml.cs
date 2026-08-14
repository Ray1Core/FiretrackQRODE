using Firetrack.ViewModels;
using Microsoft.Maui.Controls;

namespace Firetrack.Views;

public partial class CategoryItemsPage : ContentPage, IQueryAttributable
{
    private CategoryItemsViewModel _viewModel;

    public CategoryItemsPage()
    {
        InitializeComponent();
        _viewModel = new CategoryItemsViewModel("", "inventory");
        BindingContext = _viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("categoryName", out var nameObj) && nameObj is string categoryName)
        {
            _viewModel.CategoryName = categoryName;
            if (query.TryGetValue("mode", out var modeObj) && modeObj is string mode)
                _viewModel.Mode = mode;
            _viewModel.LoadItemsCommand.Execute(null);
        }
    }
}