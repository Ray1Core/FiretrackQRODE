using System;
using System.Windows.Input;
using System.Threading.Tasks;
using Firetrack.Models;
using Firetrack.Helpers;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;

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
                await Shell.Current.GoToAsync(Routes.ForgotPassword));
        }

        private async void OnLogin()
        {
            ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Username))
            {
                ErrorMessage = "Please enter your username.";
                return;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please enter your password.";
                return;
            }

            IsBusy = true;

            try
            {
                var db = App.Database;
                if (db == null)
                {
                    ErrorMessage = "Database not available. Please restart the app.";
                    System.Diagnostics.Debug.WriteLine("❌ Login failed: Database is null.");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"🔍 Attempting login for user: {Username}");
                var user = await db.GetUserByUsernameAsync(Username);

                if (user != null && user.Password == Password)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Login successful for {Username} (Role: {user.Role})");

                    // ---- Set current user ----
                    App.CurrentUser = user;

                    // ---- Log the action ----
                    await db.LogActionAsync(
                        user.Username,
                        "Login",
                        $"User logged in from {DeviceInfo.Platform}");

                    // ---- Refresh flyout visibility ----
                    if (Shell.Current is AppShell shell)
                    {
                        shell.UpdateUserRoleVisibility();

                        // 🔥 Force the flyout to close and reopen so it picks up the new IsAdmin/IsPersonnel bindings
                        Shell.Current.FlyoutIsPresented = false;
                        // A small delay helps the UI settle; then we can optionally open it again if needed
                        await Task.Delay(100);
                        // You can uncomment the next line if you want the flyout to open automatically after login
                        // Shell.Current.FlyoutIsPresented = true;
                    }

                    // ---- Navigate to the correct dashboard ----
                    string dashboardRoute = user.Role == "Admin" ? "//AdminDashboard" : "//PersonnelDashboard";
                    await Shell.Current.GoToAsync(dashboardRoute);
                }
                else
                {
                    ErrorMessage = "Invalid username or password.";
                    System.Diagnostics.Debug.WriteLine($"❌ Login failed for {Username}: Invalid credentials.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ LOGIN EXCEPTION: {ex}");
                ErrorMessage = $"Login error: {ex.Message}";
                try
                {
                    await Shell.Current.DisplayAlert("Login Error", ErrorMessage, "OK");
                }
                catch { /* Ignore if Shell is not ready */ }
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}