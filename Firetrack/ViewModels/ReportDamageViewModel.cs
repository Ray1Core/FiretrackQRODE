using Firetrack.Models;
using Firetrack.Services;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.IO;
using System.Threading.Tasks;

namespace Firetrack.ViewModels
{
    public class ReportDamageViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private EquipmentModel? _equipment;
        private string _remarks = string.Empty;
        private string _photoPath = string.Empty;
        private ImageSource? _photoPreview;
        private bool _isBusy;

        public EquipmentModel Equipment
        {
            get => _equipment!;
            set { _equipment = value; OnPropertyChanged(); }
        }

        public string Remarks
        {
            get => _remarks;
            set { _remarks = value; OnPropertyChanged(); }
        }

        public string PhotoPath
        {
            get => _photoPath;
            set { _photoPath = value; OnPropertyChanged(); }
        }

        public ImageSource? PhotoPreview
        {
            get => _photoPreview;
            set { _photoPreview = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand PickPhotoCommand { get; }
        public ICommand SubmitReportCommand { get; }
        // GoBackCommand removed – navigation handled by Shell

        public ReportDamageViewModel(EquipmentModel equipment)
        {
            _db = App.Database!;
            Equipment = equipment;

            PickPhotoCommand = new Command(OnPickPhoto);
            SubmitReportCommand = new Command(OnSubmitReport);
            // GoBackCommand assignment removed
        }

        private async void OnPickPhoto()
        {
            try
            {
                var photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "Pick a photo of the damaged equipment"
                });

                if (photo == null)
                    return;

                IsBusy = true;

                var localPath = await SavePhotoAsync(photo);
                if (!string.IsNullOrEmpty(localPath))
                {
                    PhotoPath = localPath;
                    PhotoPreview = ImageSource.FromFile(localPath);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Error picking photo: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task<string> SavePhotoAsync(FileResult photo)
        {
            var fileName = $"damage_{Equipment.QRCode}_{DateTime.Now:yyyyMMddHHmmss}.jpg";
            var appDataDir = FileSystem.AppDataDirectory;
            var savePath = Path.Combine(appDataDir, "DamagePhotos");

            if (!Directory.Exists(savePath))
                Directory.CreateDirectory(savePath);

            var fullPath = Path.Combine(savePath, fileName);

            using var stream = await photo.OpenReadAsync();
            using var fileStream = File.Create(fullPath);
            await stream.CopyToAsync(fileStream);

            return fullPath;
        }

        private async void OnSubmitReport()
        {
            if (string.IsNullOrWhiteSpace(PhotoPath))
            {
                await Shell.Current.DisplayAlert("Validation", "Please take or select a photo of the damage.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(Remarks))
            {
                await Shell.Current.DisplayAlert("Validation", "Please add remarks describing the damage.", "OK");
                return;
            }

            IsBusy = true;

            try
            {
                Equipment.Status = "Damaged";
                Equipment.PhotoPath = PhotoPath;
                Equipment.Remarks = Remarks;
                Equipment.LastUpdated = DateTime.Now;

                var transaction = new TransactionModel
                {
                    EquipmentQR = Equipment.QRCode,
                    FromUser = App.CurrentUser?.Username ?? "unknown",
                    ToUser = Equipment.AssignedToUsername ?? "none",
                    Timestamp = DateTime.Now,
                    Action = "ReportDamage",
                    Remarks = Remarks
                };

                await _db.SaveEquipmentAsync(Equipment);
                await _db.SaveTransactionAsync(transaction);

                await _db.SendNotificationAsync(
                    "admin",
                    "⚠️ Damage Report",
                    $"{App.CurrentUser?.FullName} reported damage on '{Equipment.Name}'."
                );

                await Shell.Current.DisplayAlert("Success", "Damage report submitted successfully!", "OK");
                // Navigate to Dashboard after submission
                await Shell.Current.GoToAsync("//DashboardPage");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}