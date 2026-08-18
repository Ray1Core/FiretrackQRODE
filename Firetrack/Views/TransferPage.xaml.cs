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
        if (query.TryGetValue("scannedQR", out var qrObj) && qrObj is string qrCode)
        {
            // ✅ Safe null handling for mode
            string mode = "equipment";
            if (query.TryGetValue("mode", out var modeObj) && modeObj is string modeStr)
            {
                mode = modeStr;
            }
            await _viewModel.ProcessScannedQR(qrCode, mode);
        }
    }
}