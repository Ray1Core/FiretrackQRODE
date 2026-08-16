using Firetrack.Models;
using Firetrack.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class UserManagementViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;
        private ObservableCollection<UserModel> _users = new();
        private bool _isBusy;
        private bool _isAddingUser;

        // Add user fields
        private string _newUsername = string.Empty;
        private string _newPassword = string.Empty;
        private string _newFullName = string.Empty;
        private string _newRole = "Personnel";
        private string _addStatusMessage = string.Empty;

        public ObservableCollection<UserModel> Users
        {
            get => _users;
            set { _users = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public bool IsAddingUser
        {
            get => _isAddingUser;
            set { _isAddingUser = value; OnPropertyChanged(); }
        }

        public string NewUsername
        {
            get => _newUsername;
            set { _newUsername = value; OnPropertyChanged(); }
        }

        public string NewPassword
        {
            get => _newPassword;
            set { _newPassword = value; OnPropertyChanged(); }
        }

        public string NewFullName
        {
            get => _newFullName;
            set { _newFullName = value; OnPropertyChanged(); }
        }

        public string NewRole
        {
            get => _newRole;
            set { _newRole = value; OnPropertyChanged(); }
        }

        public string AddStatusMessage
        {
            get => _addStatusMessage;
            set { _addStatusMessage = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> Roles { get; } = new() { "Admin", "Personnel" };

        public ICommand LoadUsersCommand { get; }
        public ICommand ToggleAddFormCommand { get; }
        public ICommand SaveUserCommand { get; }
        public ICommand CancelAddCommand { get; }
        public ICommand ToggleActiveCommand { get; }
        public ICommand ResetPasswordCommand { get; }
        public ICommand EditRoleCommand { get; }

        public UserManagementViewModel()
        {
            _db = App.Database!;

            LoadUsersCommand = new Command(async () => await OnLoadUsers());
            ToggleAddFormCommand = new Command(() => IsAddingUser = !IsAddingUser);
            SaveUserCommand = new Command(OnSaveUser);
            CancelAddCommand = new Command(CancelAdd);
            ToggleActiveCommand = new Command<UserModel>(OnToggleActive);
            ResetPasswordCommand = new Command<UserModel>(OnResetPassword);
            EditRoleCommand = new Command<UserModel>(OnEditRole);

            Task.Run(async () => await OnLoadUsers());
        }

        // ✅ FIX: Changed from async void to async Task
        private async Task OnLoadUsers()
        {
            if (_db == null) return;

            IsBusy = true;
            try
            {
                var list = await _db.GetUsersAsync();
                Users.Clear();
                foreach (var u in list)
                    Users.Add(u);
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

        private void CancelAdd()
        {
            IsAddingUser = false;
            ClearAddFields();
            AddStatusMessage = string.Empty;
        }

        private void ClearAddFields()
        {
            NewUsername = string.Empty;
            NewPassword = string.Empty;
            NewFullName = string.Empty;
            NewRole = "Personnel";
        }

        private async void OnSaveUser()
        {
            if (string.IsNullOrWhiteSpace(NewUsername) || string.IsNullOrWhiteSpace(NewPassword) || string.IsNullOrWhiteSpace(NewFullName))
            {
                AddStatusMessage = "All fields are required.";
                return;
            }

            IsBusy = true;
            AddStatusMessage = string.Empty;

            try
            {
                var existing = await _db.GetUserByUsernameAsync(NewUsername);
                if (existing != null)
                {
                    AddStatusMessage = "Username already exists.";
                    IsBusy = false;
                    return;
                }

                var newUser = new UserModel
                {
                    Username = NewUsername.Trim(),
                    Password = NewPassword.Trim(),
                    FullName = NewFullName.Trim(),
                    Role = NewRole
                };

                await _db.SaveUserAsync(newUser);

                if (App.CurrentUser != null)
                {
                    await _db.LogActionAsync(
                        App.CurrentUser.Username,
                        "Add User",
                        $"Added user '{newUser.Username}'");
                }

                AddStatusMessage = "✅ User created successfully!";
                ClearAddFields();
                IsAddingUser = false;
                await OnLoadUsers(); // ✅ Now works – OnLoadUsers returns Task
            }
            catch (Exception ex)
            {
                AddStatusMessage = $"❌ Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ---- Existing user actions (unchanged) ----
        private async void OnToggleActive(UserModel? user)
        {
            if (user == null) return;
            if (user.Username == "admin")
            {
                await Shell.Current.DisplayAlert("Warning", "Cannot deactivate the main admin account.", "OK");
                return;
            }

            user.IsActive = !user.IsActive;
            try
            {
                await _db.UpdateUserAsync(user);
                await Shell.Current.DisplayAlert("Success", $"User '{user.Username}' is now {(user.IsActive ? "Active" : "Inactive")}.", "OK");
                await OnLoadUsers(); // ✅ Fixed
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
                user.IsActive = !user.IsActive;
            }
        }

        private async void OnResetPassword(UserModel? user)
        {
            if (user == null) return;

            string newPassword = await Shell.Current.DisplayPromptAsync(
                "Reset Password",
                $"Enter new password for {user.Username}:",
                "Save",
                "Cancel",
                placeholder: "New password",
                maxLength: 20);

            if (string.IsNullOrWhiteSpace(newPassword))
                return;

            try
            {
                bool success = await _db.ResetPasswordAsync(user.Username, newPassword);
                if (success)
                    await Shell.Current.DisplayAlert("Success", $"Password for {user.Username} has been reset.", "OK");
                else
                    await Shell.Current.DisplayAlert("Error", "Failed to reset password.", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnEditRole(UserModel? user)
        {
            if (user == null) return;
            if (user.Username == "admin")
            {
                await Shell.Current.DisplayAlert("Warning", "Cannot change role of the main admin account.", "OK");
                return;
            }

            string newRole = await Shell.Current.DisplayActionSheet(
                "Select Role",
                "Cancel",
                null,
                "Admin",
                "Personnel");

            if (string.IsNullOrEmpty(newRole) || newRole == "Cancel")
                return;

            user.Role = newRole;
            try
            {
                await _db.UpdateUserAsync(user);
                await Shell.Current.DisplayAlert("Success", $"Role for {user.Username} set to {newRole}.", "OK");
                await OnLoadUsers(); // ✅ Fixed
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }
    }
}