using Firetrack.Models;
using Firetrack.ViewModels;
using Microsoft.Maui.Controls;

namespace Firetrack.Views;

public partial class CategoryItemsPage : ContentPage, IQueryAttributable
{
    private readonly CategoryItemsViewModel _viewModel;

    public CategoryItemsPage()
    {
        InitializeComponent();
        _viewModel = new CategoryItemsViewModel("");
        BindingContext = _viewModel;
    }

    // Fires whenever the page becomes visible — including when the
    // user pops back via GoToAsync("..") from the detail page.
    // Without this, the item you just requested stays visible until
    // you navigate out of the category entirely and back in.
    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!string.IsNullOrEmpty(_viewModel.CategoryName))
            _viewModel.LoadItemsCommand.Execute(null);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("categoryName", out var nameObj) && nameObj is string categoryName)
        {
            _viewModel.CategoryName = categoryName;
            _viewModel.LoadItemsCommand.Execute(null);
        }
    }
}