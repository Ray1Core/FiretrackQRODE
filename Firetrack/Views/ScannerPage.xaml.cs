using Firetrack.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace Firetrack.Views;

public partial class ScannerPage : ContentPage
{
    private ScannerViewModel _viewModel;

    public ScannerPage()
    {
        InitializeComponent();
        _viewModel = new ScannerViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Request camera permission
        var status = await Permissions.RequestAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert("Permission Denied", "Camera permission is required to scan QR codes.", "OK");
            await Shell.Current.GoToAsync("..");
            return;
        }

        // ✅ CRITICAL: Set formats to QR only (fix for 0.4.0)
        cameraBarcodeReaderView.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.TwoDimensional   // QR Codes only
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