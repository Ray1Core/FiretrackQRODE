using System;
using System.Windows.Input;
using System.Threading.Tasks;
using Firetrack.Models;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;  // ✅ for DeviceInfo

namespace Firetrack.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _isBusy;

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        public string Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand LoginCommand { get; }
        public ICommand GoToForgotPasswordCommand { get; }

        public LoginViewModel()
        {
            LoginCommand = new Command(OnLogin);
            GoToForgotPasswordCommand = new Command(async () =>
                await Shell.Current.GoToAsync("//ForgotPasswordPage"));
        }

        private async void OnLogin()
        {
            try
            {
                var db = App.Database;
                if (db == null)
                {
                    ErrorMessage = "Database not available.";
                    return;
                }

                IsBusy = true;
                ErrorMessage = string.Empty;

                var user = await db.GetUserByUsernameAsync(Username);
                if (user != null && user.Password == Password)
                {
                    // Set current user
                    App.CurrentUser = user;

                    // ✅ Log successful login
                    await db.LogActionAsync(
                        user.Username,
                        "Login",
                        $"User logged in from {DeviceInfo.Platform}");

                    // Update Shell flyout visibility based on role
                    if (Shell.Current is AppShell shell)
                    {
                        shell.UpdateUserRoleVisibility();
                    }

                    // Clear navigation stack and navigate to Dashboard
                    await Shell.Current.GoToAsync("//DashboardPage");
                }
                else
                {
                    ErrorMessage = "Invalid username or password";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}