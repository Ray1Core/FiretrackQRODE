using Firetrack.Models;
using Firetrack.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.IO;

namespace Firetrack.ViewModels
{
    public class DisposalRequestsViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private ObservableCollection<EquipmentModel> _pendingRequests = new();
        private EquipmentModel? _selectedRequest;
        private string _statusMessage = string.Empty;
        private bool _isBusy;

        public ObservableCollection<EquipmentModel> PendingRequests
        {
            get => _pendingRequests;
            set { _pendingRequests = value; OnPropertyChanged(); }
        }

        public EquipmentModel? SelectedRequest
        {
            get => _selectedRequest;
            set { _selectedRequest = value; OnPropertyChanged(); }
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

        public ICommand LoadRequestsCommand { get; }
        public ICommand ApproveCommand { get; }
        public ICommand RejectCommand { get; }
        public ICommand GenerateCertificateCommand { get; }

        public DisposalRequestsViewModel()
        {
            _db = App.Database!;
            LoadRequestsCommand = new Command(OnLoadRequests);
            ApproveCommand = new Command<EquipmentModel>(OnApprove);
            RejectCommand = new Command<EquipmentModel>(OnReject);
            GenerateCertificateCommand = new Command<EquipmentModel>(OnGenerateCertificate);
            OnLoadRequests();
        }

        private async void OnLoadRequests()
        {
            if (_db == null) return;

            IsBusy = true;
            try
            {
                var requests = await _db.GetDisposalRequestsAsync("Pending");
                PendingRequests.Clear();
                foreach (var req in requests)
                    PendingRequests.Add(req);
                StatusMessage = requests.Any() ? $"{requests.Count} pending disposal request(s)." : "No pending disposal requests.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void OnApprove(EquipmentModel? equipment)
        {
            if (equipment == null)
            {
                StatusMessage = "Please select a request.";
                return;
            }

            bool confirm = await Shell.Current.DisplayAlert("Approve Disposal",
                $"Approve disposal of '{equipment.Name}'? This will mark it as Disposed.",
                "Yes", "No");
            if (!confirm) return;

            string remarks = await Shell.Current.DisplayPromptAsync("Remarks",
                "Enter any remarks (optional):", "OK", "Cancel");
            if (remarks == null) return;

            IsBusy = true;
            try
            {
                var admin = App.CurrentUser;
                if (admin == null)
                {
                    StatusMessage = "Admin not logged in.";
                    IsBusy = false;
                    return;
                }

                bool success = await _db.ApproveDisposalAsync(equipment.QRCode, admin.Username, remarks);
                if (success)
                {
                    StatusMessage = $"✅ Disposal approved for '{equipment.Name}'.";
                    OnLoadRequests(); // refresh list

                    var certResult = await Shell.Current.DisplayAlert("Generate Certificate",
                        "Generate disposal certificate now?", "Yes", "No");
                    if (certResult)
                    {
                        var updated = await _db.GetEquipmentByQRAsync(equipment.QRCode);
                        if (updated != null)
                            await GenerateCertificate(updated);
                    }
                }
                else
                {
                    StatusMessage = "❌ Failed to approve disposal.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void OnReject(EquipmentModel? equipment)
        {
            if (equipment == null)
            {
                StatusMessage = "Please select a request.";
                return;
            }

            bool confirm = await Shell.Current.DisplayAlert("Reject Disposal",
                $"Reject disposal of '{equipment.Name}'?", "Yes", "No");
            if (!confirm) return;

            string remarks = await Shell.Current.DisplayPromptAsync("Reason for Rejection",
                "Enter reason (optional):", "OK", "Cancel");
            if (remarks == null) return;

            IsBusy = true;
            try
            {
                var admin = App.CurrentUser;
                if (admin == null)
                {
                    StatusMessage = "Admin not logged in.";
                    IsBusy = false;
                    return;
                }

                bool success = await _db.RejectDisposalAsync(equipment.QRCode, admin.Username, remarks);
                if (success)
                {
                    StatusMessage = $"✅ Disposal rejected for '{equipment.Name}'.";
                    OnLoadRequests(); // refresh list
                }
                else
                {
                    StatusMessage = "❌ Failed to reject disposal.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void OnGenerateCertificate(EquipmentModel? equipment)
        {
            if (equipment == null)
            {
                StatusMessage = "Please select a request.";
                return;
            }

            if (equipment.DisposalStatus != "Approved")
            {
                StatusMessage = "Disposal must be approved first.";
                return;
            }

            await GenerateCertificate(equipment);
        }

        private async Task GenerateCertificate(EquipmentModel equipment)
        {
            try
            {
                var admin = App.CurrentUser;
                if (admin == null)
                {
                    StatusMessage = "Admin not logged in.";
                    return;
                }

                var pdfService = new PdfGenerationService();
                var pdfBytes = pdfService.GenerateDisposalCertificate(equipment, admin, equipment.DisposalRemarks ?? "");

                var fileName = $"Disposal_{equipment.QRCode}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                var downloadsPath = Path.Combine(FileSystem.AppDataDirectory, "DisposalCertificates");
                Directory.CreateDirectory(downloadsPath);
                var filePath = Path.Combine(downloadsPath, fileName);
                await File.WriteAllBytesAsync(filePath, pdfBytes);

                // Open with fallback
                try
                {
                    await Launcher.Default.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(filePath) });
                }
                catch
                {
                    await Share.Default.RequestAsync(new ShareFileRequest
                    {
                        Title = "View Disposal Certificate",
                        File = new ShareFile(filePath)
                    });
                }

                StatusMessage = $"📄 Disposal certificate generated for '{equipment.Name}'.";

                // ✅ Refresh the pending requests list
                OnLoadRequests();
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Certificate generation failed: {ex.Message}";
            }
        }
    }
}