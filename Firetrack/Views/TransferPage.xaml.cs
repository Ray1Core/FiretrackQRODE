using Firetrack.ViewModels;
using Microsoft.Maui.Controls;

namespace Firetrack.Views;

public partial class TransferPage : ContentPage, IQueryAttributable
{
    private TransferViewModel _viewModel;

    public TransferPage()
    {
        InitializeComponent();
        _viewModel = new TransferViewModel();
        BindingContext = _viewModel;
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        // Called when returning from ScannerPage with scanned QR
        if (query.TryGetValue("scannedQR", out var qrObj) && qrObj is string qrCode)
        {
            await _viewModel.ProcessScannedQR(qrCode);
        }
    }
}