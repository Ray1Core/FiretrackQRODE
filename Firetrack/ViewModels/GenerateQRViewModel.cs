using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using QRCoder;

namespace Firetrack.ViewModels
{
    public class GenerateQRViewModel : ViewModelBase
    {
        private string _inputText = string.Empty;
        private ImageSource? _qrImageSource;
        private string _statusMessage = string.Empty;
        private bool _isBusy;
        private bool _isImageVisible;

        public string InputText
        {
            get => _inputText;
            set { _inputText = value; OnPropertyChanged(); }
        }

        public ImageSource? QrImageSource
        {
            get => _qrImageSource;
            set { _qrImageSource = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public bool IsImageVisible
        {
            get => _isImageVisible;
            set { _isImageVisible = value; OnPropertyChanged(); }
        }

        public ICommand GenerateCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ShareCommand { get; }

        private byte[]? _generatedPngBytes;

        public GenerateQRViewModel()
        {
            GenerateCommand = new Command(OnGenerate);
            SaveCommand = new Command(OnSave);
            ShareCommand = new Command(OnShare);
        }

        private async void OnGenerate()
        {
            if (string.IsNullOrWhiteSpace(InputText))
            {
                StatusMessage = "Please enter text to encode.";
                IsImageVisible = false;
                return;
            }

            IsBusy = true;
            StatusMessage = string.Empty;
            IsImageVisible = false;

            try
            {
                var input = InputText.Trim();

                // ✅ Run CPU-bound QR generation on background thread
                byte[] pngBytes = await Task.Run(() =>
                {
                    var generator = new QRCodeGenerator();
                    var qrCodeData = generator.CreateQrCode(input, QRCodeGenerator.ECCLevel.Q);
                    var qrCode = new PngByteQRCode(qrCodeData);
                    return qrCode.GetGraphic(20); // 20 pixels per module
                });

                _generatedPngBytes = pngBytes;
                QrImageSource = ImageSource.FromStream(() => new MemoryStream(pngBytes));
                IsImageVisible = true;
                StatusMessage = "✅ QR code generated successfully!";
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Generation failed: {ex.Message}";
                IsImageVisible = false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void OnSave()
        {
            if (_generatedPngBytes == null || _generatedPngBytes.Length == 0)
            {
                StatusMessage = "Please generate a QR code first.";
                return;
            }

            try
            {
                var fileName = $"QR_{InputText.Trim()}_{DateTime.Now:yyyyMMddHHmmss}.png";
                var downloadsPath = Path.Combine(FileSystem.AppDataDirectory, "QRCodes");

                if (!Directory.Exists(downloadsPath))
                    Directory.CreateDirectory(downloadsPath);

                var filePath = Path.Combine(downloadsPath, fileName);
                await File.WriteAllBytesAsync(filePath, _generatedPngBytes);

                StatusMessage = $"✅ Saved to: {filePath}";
                await Shell.Current.DisplayAlert("Success", $"QR code saved to:\n{filePath}", "OK");
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Save failed: {ex.Message}";
            }
        }

        private async void OnShare()
        {
            if (_generatedPngBytes == null || _generatedPngBytes.Length == 0)
            {
                StatusMessage = "Please generate a QR code first.";
                return;
            }

            try
            {
                var fileName = $"QR_{InputText.Trim()}_{DateTime.Now:yyyyMMddHHmmss}.png";
                var tempPath = Path.Combine(Path.GetTempPath(), fileName);
                await File.WriteAllBytesAsync(tempPath, _generatedPngBytes);

                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Share QR Code",
                    File = new ShareFile(tempPath)
                });

                StatusMessage = "✅ QR code shared!";
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Share failed: {ex.Message}";
            }
        }
    }
}