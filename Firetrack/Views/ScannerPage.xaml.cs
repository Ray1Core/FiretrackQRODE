using Firetrack.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace Firetrack.Views;

public partial class ScannerPage : ContentPage, IQueryAttributable
{
    private ScannerViewModel _viewModel;

    public ScannerPage()
    {
        InitializeComponent();
        _viewModel = new ScannerViewModel();
        BindingContext = _viewModel;
    }

    // ✅ Receives query parameters from navigation
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("returnTo", out var returnToObj) && returnToObj is string returnTo)
        {
            _viewModel.ReturnToPage = returnTo;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Request camera permission
        var status = await Permissions.RequestAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert("Permission Denied", "Camera permission is required to scan QR codes.", "OK");
            // Navigate to returnTo page or Dashboard
            if (!string.IsNullOrEmpty(_viewModel.ReturnToPage))
                await Shell.Current.GoToAsync($"//{_viewModel.ReturnToPage}");
            else
                await Shell.Current.GoToAsync("//DashboardPage");
            return;
        }

        cameraBarcodeReaderView.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.TwoDimensional
        };

        _viewModel.IsScanning = true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.IsScanning = false;
    }

    private async void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        var result = e.Results?.FirstOrDefault();
        if (result == null || string.IsNullOrEmpty(result.Value))
            return;

        _viewModel.IsScanning = false;
        await _viewModel.ProcessScannedQR(result.Value);
    }
}