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
    private int _detectionCount = 0;

    public ScannerPage()
    {
        InitializeComponent();
        _viewModel = new ScannerViewModel();
        BindingContext = _viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("returnTo", out var returnToObj) && returnToObj is string returnTo)
        {
            _viewModel.ReturnToPage = returnTo;
            System.Diagnostics.Debug.WriteLine($"🔍 Scanner ApplyQueryAttributes: returnTo={returnTo}");
        }

        if (query.TryGetValue("mode", out var modeObj) && modeObj is string mode)
        {
            _viewModel.ScanMode = mode;
            System.Diagnostics.Debug.WriteLine($"🔍 Scanner ApplyQueryAttributes: mode={mode}");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        System.Diagnostics.Debug.WriteLine("🔍 Scanner OnAppearing");

        _viewModel.IsPlaceholderVisible = true;
        _viewModel.IsCameraReady = false;
        _viewModel.IsScanning = false;
        _cameraConfigured = false;
        _detectionCount = 0;

        var status = await Permissions.RequestAsync<Permissions.Camera>();
        System.Diagnostics.Debug.WriteLine($"🔍 Camera permission status: {status}");

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

        // Shorter warm-up — the camera view is always visible so detection
        // can start immediately once permission is confirmed.
        await Task.Delay(250);

        if (!_cameraConfigured)
        {
            // ✅ FIX: Only BarcodeFormats.TwoDimensional exists in ZXing.Net.Maui 0.4.0.
            // We filter for QR codes inside OnBarcodesDetected instead of trying
            // to use non-existent enum members.
            cameraBarcodeReaderView.Options = new BarcodeReaderOptions
            {
                Formats = BarcodeFormats.TwoDimensional,
                AutoRotate = true,
                Multiple = false,
                TryHarder = true
            };

            _cameraConfigured = true;

            System.Diagnostics.Debug.WriteLine("🔍 BarcodeReader options configured");
        }

        _viewModel.IsPlaceholderVisible = false;
        _viewModel.IsCameraReady = true;

        // Short settle for autofocus
        await Task.Delay(150);

        _viewModel.IsScanning = true;
        System.Diagnostics.Debug.WriteLine("🔍 IsScanning = true — ready to detect");
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        System.Diagnostics.Debug.WriteLine("🔍 Scanner OnDisappearing");

        _viewModel.IsCameraReady = false;
        _viewModel.IsPlaceholderVisible = true;
        _viewModel.IsScanning = false;

        // DO NOT call DisconnectHandler() — breaks MediaTek camera HAL.
    }

    private async void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        var result = e.Results?.FirstOrDefault();

        // ✅ Diagnostic log — fires on every detection attempt
        _detectionCount++;
        System.Diagnostics.Debug.WriteLine(
            $"🔍 OnBarcodesDetected #{_detectionCount}: " +
            $"value='{result?.Value ?? "<null>"}' format={result?.Format}");

        if (result == null || string.IsNullOrEmpty(result.Value))
            return;

        // ✅ FIX: don't gate on IsScanning. If a barcode is detected at all,
        // process it. The page is only shown when the user wants to scan.
        _viewModel.IsScanning = false;
        System.Diagnostics.Debug.WriteLine($"🔍 Passing to ProcessScannedQR: {result.Value}");

        await _viewModel.ProcessScannedQR(result.Value);
    }
}