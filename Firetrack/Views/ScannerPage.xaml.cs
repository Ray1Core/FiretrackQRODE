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

        // 1. Reset the UI state to show the placeholder
        _viewModel.IsPlaceholderVisible = true;
        _viewModel.IsCameraReady = false;

        // 2. Request Camera Permission
        var status = await Permissions.RequestAsync<Permissions.Camera>();

        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert("Permission Denied", "Camera permission is required to scan QR codes.", "OK");

            // Navigate back safely
            if (!string.IsNullOrEmpty(_viewModel.ReturnToPage))
                await Shell.Current.GoToAsync($"//{_viewModel.ReturnToPage}");
            else
                await Shell.Current.GoToAsync(Routes.GetDashboardRoute());
            return;
        }

        // 3. Permission Granted - Initialize Camera
        // ✅ FIX for Realme C100 4G black screen: 
        // A short delay allows the Android camera HAL to fully release/re-acquire 
        // when navigating back to this page, preventing a black screen.
        await Task.Delay(200);

        // 4. Show Camera and Hide Placeholder via ViewModel
        _viewModel.IsPlaceholderVisible = false;
        _viewModel.IsCameraReady = true;

        // 5. Configure ZXing Options
        cameraBarcodeReaderView.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.TwoDimensional
        };

        // 6. Resume scanning
        _viewModel.IsScanning = true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // ✅ FIX: Hide the camera view BEFORE disconnecting the handler.
        // This ensures the native Android view is fully detached before the page is destroyed.
        _viewModel.IsCameraReady = false;
        _viewModel.IsPlaceholderVisible = true;
        _viewModel.IsScanning = false;

        // Release the camera hardware
        cameraBarcodeReaderView.Handler?.DisconnectHandler();
    }

    private async void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        var result = e.Results?.FirstOrDefault();
        if (result == null || string.IsNullOrEmpty(result.Value))
            return;

        // Prevent processing if already busy or not scanning
        if (!_viewModel.IsScanning)
            return;

        // Pause further detections
        _viewModel.IsScanning = false;

        // Process the scanned QR
        await _viewModel.ProcessScannedQR(result.Value);
    }
}