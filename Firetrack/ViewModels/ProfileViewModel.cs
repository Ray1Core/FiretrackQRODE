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
        private bool _isBusy;

        public string FullName => _currentUser?.FullName ?? "Unknown";
        public string Username => _currentUser?.Username ?? "Unknown";
        public string Role => _currentUser?.Role ?? "Unknown";
        public string Status => _currentUser?.IsActive == true ? "Active" : "Inactive";

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand ChangePasswordCommand { get; }
        public ICommand LogoutCommand { get; }

        public ProfileViewModel()
        {
            _db = App.Database!;
            _currentUser = App.CurrentUser;
            ChangePasswordCommand = new Command(OnChangePassword);
            LogoutCommand = new Command(OnLogout);
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