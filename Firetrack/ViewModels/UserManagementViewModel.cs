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
        private string _newEmail = string.Empty;          // Username → Email
        private string _newPassword = string.Empty;       // Password → PasswordHash
        private string _newFirstName = string.Empty;      // New: First Name
        private string _newLastName = string.Empty;       // New: Last Name
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

        // New properties for add form
        public string NewEmail
        {
            get => _newEmail;
            set { _newEmail = value; OnPropertyChanged(); }
        }

        public string NewPassword
        {
            get => _newPassword;
            set { _newPassword = value; OnPropertyChanged(); }
        }

        public string NewFirstName
        {
            get => _newFirstName;
            set { _newFirstName = value; OnPropertyChanged(); }
        }

        public string NewLastName
        {
            get => _newLastName;
            set { _newLastName = value; OnPropertyChanged(); }
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
            NewEmail = string.Empty;
            NewPassword = string.Empty;
            NewFirstName = string.Empty;
            NewLastName = string.Empty;
            NewRole = "Personnel";
        }

        private async void OnSaveUser()
        {
            if (string.IsNullOrWhiteSpace(NewEmail) ||
                string.IsNullOrWhiteSpace(NewPassword) ||
                string.IsNullOrWhiteSpace(NewFirstName) ||
                string.IsNullOrWhiteSpace(NewLastName))
            {
                AddStatusMessage = "All fields are required.";
                return;
            }

            IsBusy = true;
            AddStatusMessage = string.Empty;

            try
            {
                // Check if email (username) exists using the new GetUserByEmailAsync
                var existing = await _db.GetUserByEmailAsync(NewEmail.Trim());
                if (existing != null)
                {
                    AddStatusMessage = "Email already exists.";
                    IsBusy = false;
                    return;
                }

                // Create new user with the new schema
                var newUser = new UserModel
                {
                    Email = NewEmail.Trim(),
                    PasswordHash = NewPassword.Trim(),   // In production, hash this
                    FirstName = NewFirstName.Trim(),
                    LastName = NewLastName.Trim(),
                    Role = NewRole,
                    Status = "Active"
                };

                await _db.SaveUserAsync(newUser);

                if (App.CurrentUser != null)
                {
                    await _db.LogActionAsync(
                        App.CurrentUser.Email,
                        "Add User",
                        $"Added user '{newUser.Email}'");
                }

                AddStatusMessage = "✅ User created successfully!";
                ClearAddFields();
                IsAddingUser = false;
                await OnLoadUsers();
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

        // ---- Toggle Active ----
        private async void OnToggleActive(UserModel? user)
        {
            if (user == null) return;
            if (user.Email == "admin@firetrack.gov")  // protect main admin
            {
                await Shell.Current.DisplayAlert("Warning", "Cannot deactivate the main admin account.", "OK");
                return;
            }

            // Toggle Status
            user.Status = user.Status == "Active" ? "Inactive" : "Active";
            try
            {
                await _db.UpdateUserAsync(user);
                await Shell.Current.DisplayAlert("Success", $"User '{user.Email}' is now {(user.Status == "Active" ? "Active" : "Inactive")}.", "OK");
                await OnLoadUsers();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
                // revert
                user.Status = user.Status == "Active" ? "Inactive" : "Active";
            }
        }

        // ---- Reset Password ----
        private async void OnResetPassword(UserModel? user)
        {
            if (user == null) return;

            string newPassword = await Shell.Current.DisplayPromptAsync(
                "Reset Password",
                $"Enter new password for {user.Email}:",
                "Save",
                "Cancel",
                placeholder: "New password",
                maxLength: 20);

            if (string.IsNullOrWhiteSpace(newPassword))
                return;

            try
            {
                bool success = await _db.ResetPasswordAsync(user.Email, newPassword);
                if (success)
                    await Shell.Current.DisplayAlert("Success", $"Password for {user.Email} has been reset.", "OK");
                else
                    await Shell.Current.DisplayAlert("Error", "Failed to reset password.", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // ---- Edit Role ----
        private async void OnEditRole(UserModel? user)
        {
            if (user == null) return;
            if (user.Email == "admin@firetrack.gov")
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
                await Shell.Current.DisplayAlert("Success", $"Role for {user.Email} set to {newRole}.", "OK");
                await OnLoadUsers();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }
    }
}