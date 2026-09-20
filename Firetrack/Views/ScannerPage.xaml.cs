using Firetrack.ViewModels;
using Firetrack.Helpers;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace Firetrack.Views;

public partial class ScannerPage : ContentPage, IQueryAttributable
{
    private ScannerViewModel _viewModel;
    private bool _cameraConfigured = false;

    public ScannerPage()
    {
        InitializeComponent();
        _viewModel = new ScannerViewModel();
        BindingContext = _viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("returnTo", out var returnToObj) && returnToObj is string returnTo)
            _viewModel.ReturnToPage = returnTo;

        if (query.TryGetValue("mode", out var modeObj) && modeObj is string mode)
            _viewModel.ScanMode = mode;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _viewModel.IsPlaceholderVisible = true;
        _viewModel.IsCameraReady = false;
        _viewModel.IsScanning = false;
        _cameraConfigured = false;

        var status = await Permissions.RequestAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert("Permission Denied",
                "Camera permission is required to scan QR codes.", "OK");

            if (!string.IsNullOrEmpty(_viewModel.ReturnToPage))
                await Shell.Current.GoToAsync($"//{_viewModel.ReturnToPage}");
            else
                await Shell.Current.GoToAsync(Routes.GetDashboardRoute());
            return;
        }

        // MediaTek workaround: 700ms allows camera HAL to re-acquire
        await Task.Delay(700);

        if (!_cameraConfigured)
        {
            cameraBarcodeReaderView.Options = new BarcodeReaderOptions
            {
                Formats = BarcodeFormats.TwoDimensional,
                AutoRotate = true,
                Multiple = false,
                TryHarder = true
            };
            _cameraConfigured = true;
        }

        _viewModel.IsPlaceholderVisible = false;
        _viewModel.IsCameraReady = true;

        // Let autofocus settle before accepting scans
        await Task.Delay(500);
        _viewModel.IsScanning = true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _viewModel.IsCameraReady = false;
        _viewModel.IsPlaceholderVisible = true;
        _viewModel.IsScanning = false;

        // DO NOT call DisconnectHandler() — breaks MediaTek camera HAL
    }

    private async void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        var result = e.Results?.FirstOrDefault();
        if (result == null || string.IsNullOrEmpty(result.Value))
            return;

        if (!_viewModel.IsScanning)
            return;

        _viewModel.IsScanning = false;
        await _viewModel.ProcessScannedQR(result.Value);
    }
}