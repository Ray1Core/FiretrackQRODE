using Firetrack.Models;
using Firetrack.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class AddUserViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _fullName = string.Empty;
        private string _role = "Personnel";
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

        public string FullName
        {
            get => _fullName;
            set { _fullName = value; OnPropertyChanged(); }
        }

        public string Role
        {
            get => _role;
            set { _role = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> Roles { get; } = new() { "Admin", "Personnel" };

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand SaveUserCommand { get; }

        public AddUserViewModel()
        {
            _db = App.Database!;
            SaveUserCommand = new Command(OnSaveUser);
        }

        private async void OnSaveUser()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(FullName))
            {
                await Shell.Current.DisplayAlert("Validation", "All fields are required.", "OK");
                return;
            }

            IsBusy = true;

            try
            {
                var existing = await _db.GetUserByUsernameAsync(Username);
                if (existing != null)
                {
                    await Shell.Current.DisplayAlert("Error", "Username already exists.", "OK");
                    IsBusy = false;
                    return;
                }

                var newUser = new UserModel
                {
                    Username = Username.Trim(),
                    Password = Password.Trim(),
                    FullName = FullName.Trim(),
                    Role = Role
                };

                await _db.SaveUserAsync(newUser);

                // ✅ Log the action
                if (App.CurrentUser != null)
                {
                    await _db.LogActionAsync(
                        App.CurrentUser.Username,
                        "Add User",
                        $"Added user '{newUser.Username}'");
                }

                await Shell.Current.DisplayAlert("Success", $"User '{newUser.Username}' created successfully!", "OK");

                // Clear fields
                Username = string.Empty;
                Password = string.Empty;
                FullName = string.Empty;
                Role = "Personnel";

                await Shell.Current.GoToAsync("UserManagementPage");
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