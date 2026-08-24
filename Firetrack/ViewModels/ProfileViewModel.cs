using Firetrack.Models;
using Firetrack.Services;
using Firetrack.Helpers;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.IO;

namespace Firetrack.ViewModels
{
    public class ProfileViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private UserModel? _currentUser;
        private bool _isBusy;
        private ImageSource? _profileImageSource;

        public string FullName => _currentUser?.FullName ?? "Unknown";
        public string Username => _currentUser?.Username ?? "Unknown";
        public string Role => _currentUser?.Role ?? "Unknown";
        public string Status => _currentUser?.IsActive == true ? "Active" : "Inactive";

        public ImageSource? ProfileImageSource
        {
            get => _profileImageSource;
            set { _profileImageSource = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand ChangePasswordCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand ChangeProfilePictureCommand { get; }

        public ProfileViewModel()
        {
            _db = App.Database!;
            _currentUser = App.CurrentUser;
            ChangePasswordCommand = new Command(OnChangePassword);
            LogoutCommand = new Command(OnLogout);
            ChangeProfilePictureCommand = new Command(OnChangeProfilePicture);

            LoadProfileImage();
        }

        // ===== LOAD PROFILE IMAGE =====
        private void LoadProfileImage()
        {
            if (_currentUser == null)
            {
                ProfileImageSource = ImageSource.FromFile("defaultprofile.png");
                return;
            }

            if (!string.IsNullOrEmpty(_currentUser.ProfileImagePath))
            {
                try
                {
                    var fullPath = Path.Combine(FileSystem.AppDataDirectory, _currentUser.ProfileImagePath);
                    if (File.Exists(fullPath))
                    {
                        ProfileImageSource = ImageSource.FromFile(fullPath);
                        return;
                    }
                }
                catch
                {
                    // Fall through to default
                }
            }

            // Default image (must exist in Resources/Images)
            ProfileImageSource = ImageSource.FromFile("defaultprofile.png");
        }

        // ===== CHANGE PROFILE PICTURE =====
        private async void OnChangeProfilePicture()
        {
            if (_currentUser == null)
            {
                await Shell.Current.DisplayAlert("Error", "User not logged in.", "OK");
                return;
            }

            try
            {
                var photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "Select Profile Picture"
                });

                if (photo == null)
                    return;

                IsBusy = true;

                // Save to app data
                var fileName = $"profile_{_currentUser.UserId}_{DateTime.Now:yyyyMMddHHmmss}.jpg";
                var saveDir = Path.Combine(FileSystem.AppDataDirectory, "ProfilePics");
                if (!Directory.Exists(saveDir))
                    Directory.CreateDirectory(saveDir);

                var fullPath = Path.Combine(saveDir, fileName);

                using var stream = await photo.OpenReadAsync();
                using var fileStream = File.Create(fullPath);
                await stream.CopyToAsync(fileStream);

                // Update user model
                _currentUser.ProfileImagePath = Path.Combine("ProfilePics", fileName);

                // Save to database
                await _db.UpdateUserAsync(_currentUser);

                // Update UI
                ProfileImageSource = ImageSource.FromFile(fullPath);

                await Shell.Current.DisplayAlert("Success", "Profile picture updated.", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Failed to update profile picture: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ===== CHANGE PASSWORD (unchanged) =====
        private async void OnChangePassword()
        {
            if (_currentUser == null)
            {
                await Shell.Current.DisplayAlert("Error", "User not logged in.", "OK");
                return;
            }

            string newPassword = await Shell.Current.DisplayPromptAsync(
                "Change Password",
                $"Enter new password for {_currentUser.Username}:",
                "Save",
                "Cancel",
                placeholder: "New password",
                maxLength: 20);

            if (string.IsNullOrWhiteSpace(newPassword))
                return;

            if (newPassword.Length < 4)
            {
                await Shell.Current.DisplayAlert("Error", "Password must be at least 4 characters.", "OK");
                return;
            }

            IsBusy = true;
            try
            {
                bool success = await _db.ResetPasswordAsync(_currentUser.Username, newPassword);
                if (success)
                {
                    await Shell.Current.DisplayAlert("Success", "Password changed successfully.", "OK");
                    _currentUser.Password = newPassword;
                }
                else
                {
                    await Shell.Current.DisplayAlert("Error", "Failed to change password.", "OK");
                }
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

        // ===== LOGOUT (unchanged) =====
        private async void OnLogout()
        {
            if (_currentUser != null)
            {
                await _db.LogActionAsync(_currentUser.Username, "Logout", "User logged out");
            }

            App.CurrentUser = null;
            if (Shell.Current is AppShell shell) shell.UpdateUserRoleVisibility();
            await Shell.Current.GoToAsync(Routes.Login);
        }
    }
}