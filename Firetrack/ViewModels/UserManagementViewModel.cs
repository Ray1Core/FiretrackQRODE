using Firetrack.Models;
using Firetrack.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class UserManagementViewModel : ViewModelBase
    {
        private ObservableCollection<UserModel> _users = new();
        private UserModel? _selectedUser;
        private bool _isBusy;

        public ObservableCollection<UserModel> Users
        {
            get => _users;
            set { _users = value; OnPropertyChanged(); }
        }

        public UserModel? SelectedUser
        {
            get => _selectedUser;
            set { _selectedUser = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand LoadUsersCommand { get; }
        public ICommand ToggleActiveCommand { get; }
        public ICommand ResetPasswordCommand { get; }
        public ICommand EditRoleCommand { get; }
        // GoBackCommand removed – navigation handled by Shell

        public UserManagementViewModel()
        {
            LoadUsersCommand = new Command(OnLoadUsers);
            ToggleActiveCommand = new Command<UserModel>(OnToggleActive);
            ResetPasswordCommand = new Command<UserModel>(OnResetPassword);
            EditRoleCommand = new Command<UserModel>(OnEditRole);
            // GoBackCommand assignment removed

            OnLoadUsers();
        }

        private async void OnLoadUsers()
        {
            if (App.Database == null) return;

            IsBusy = true;
            try
            {
                var list = await App.Database.GetUsersAsync();
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
                await App.Database!.UpdateUserAsync(user);
                await Shell.Current.DisplayAlert("Success", $"User '{user.Username}' is now {(user.IsActive ? "Active" : "Inactive")}.", "OK");
                OnLoadUsers(); // refresh
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
                user.IsActive = !user.IsActive; // revert
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
                bool success = await App.Database!.ResetPasswordAsync(user.Username, newPassword);
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
                await App.Database!.UpdateUserAsync(user);
                await Shell.Current.DisplayAlert("Success", $"Role for {user.Username} set to {newRole}.", "OK");
                OnLoadUsers();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }
    }
}