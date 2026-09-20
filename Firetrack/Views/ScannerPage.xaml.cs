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

        await Task.Delay(250);

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
            System.Diagnostics.Debug.WriteLine("🔍 BarcodeReader options configured");
        }

        _viewModel.IsPlaceholderVisible = false;
        _viewModel.IsCameraReady = true;

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

        _detectionCount++;
        System.Diagnostics.Debug.WriteLine(
            $"🔍 OnBarcodesDetected #{_detectionCount}: " +
            $"value='{result?.Value ?? "<null>"}' format={result?.Format}");

        if (result == null || string.IsNullOrEmpty(result.Value))
            return;

        _viewModel.IsScanning = false;
        System.Diagnostics.Debug.WriteLine($"🔍 Passing to ProcessScannedQR: {result.Value}");

        await _viewModel.ProcessScannedQR(result.Value);
    }

    // ============================================================
    // MANUAL QR ENTRY FALLBACK
    // ------------------------------------------------------------
    // MediaTek devices (Realme C100 4G, some Oppo/Vivo) fail the
    // Camera2 session config when Preview + ImageAnalysis are
    // combined, so ZXing never fires OnBarcodesDetected.
    //
    // This button lets the user type the QR code (e.g. "HOSE001")
    // manually. Since Firetrack's QR values are deterministic
    // property numbers, typing is functionally equivalent to
    // scanning and unblocks the Transfer flow on those devices.
    // ============================================================
    private async void OnTypeQrClicked(object sender, EventArgs e)
    {
        string typed = await DisplayPromptAsync(
            "Enter QR Code",
            "Type the equipment QR code exactly as printed:\n(example: HOSE001)",
            accept: "Submit",
            cancel: "Cancel",
            placeholder: "HOSE001",
            maxLength: 100);

        if (string.IsNullOrWhiteSpace(typed))
            return;

        typed = typed.Trim().ToUpperInvariant();
        System.Diagnostics.Debug.WriteLine($"🔍 Manual QR entry: {typed}");

        _viewModel.IsScanning = false;
        await _viewModel.ProcessScannedQR(typed);
    }
}