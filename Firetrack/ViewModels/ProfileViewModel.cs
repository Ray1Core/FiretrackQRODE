using Firetrack.Models;
using Firetrack.Services;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class ProfileViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private UserModel? _currentUser;

        public string FullName => _currentUser?.FullName ?? "Unknown";
        public string Username => _currentUser?.Username ?? "Unknown";
        public string Role => _currentUser?.Role ?? "Unknown";
        public string Status => _currentUser?.IsActive == true ? "Active" : "Inactive";

        public ICommand ChangePasswordCommand { get; }
        public ICommand LogoutCommand { get; }  // ✅ New

        public ProfileViewModel()
        {
            _db = App.Database!;
            _currentUser = App.CurrentUser;
            ChangePasswordCommand = new Command(OnChangePassword);
            LogoutCommand = new Command(OnLogout);  // ✅ New
        }

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
        }

        // ✅ New Logout method
        private async void OnLogout()
        {
            if (_currentUser != null)
            {
                await _db.LogActionAsync(_currentUser.Username, "Logout", "User logged out");
            }

            App.CurrentUser = null;
            if (Shell.Current is AppShell shell) shell.UpdateUserRoleVisibility();
            await Shell.Current.GoToAsync("//LoginPage");
        }
    }
}