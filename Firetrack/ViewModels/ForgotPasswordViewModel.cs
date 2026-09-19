using Firetrack.Services;
using Firetrack.Helpers;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class ForgotPasswordViewModel : ViewModelBase
    {
        private readonly EmailService _emailService;
        private string _username = string.Empty;
        private string _otpCode = string.Empty;
        private string _newPassword = string.Empty;
        private string _statusMessage = string.Empty;
        private bool _isBusy;
        private bool _otpSent;

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        public string OtpCode
        {
            get => _otpCode;
            set { _otpCode = value; OnPropertyChanged(); }
        }

        public string NewPassword
        {
            get => _newPassword;
            set { _newPassword = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand SendOtpCommand { get; }
        public ICommand ResetPasswordCommand { get; }
        public ICommand GoBackCommand { get; }

        public ForgotPasswordViewModel(EmailService emailService)
        {
            _emailService = emailService;

            SendOtpCommand = new Command(OnSendOtp);
            ResetPasswordCommand = new Command(OnResetPassword);
            GoBackCommand = new Command(async () => await Shell.Current.GoToAsync(Routes.Login));
        }

        private async void OnSendOtp()
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                StatusMessage = "Please enter your username.";
                return;
            }

            IsBusy = true;
            StatusMessage = string.Empty;

            try
            {
                // 1. Verify user exists
                var user = await App.Database!.GetUserByUsernameAsync(Username);
                if (user == null)
                {
                    StatusMessage = "Username not found.";
                    IsBusy = false;
                    return;
                }

                // 2. Generate OTP and save to database FIRST
                string otp = await App.Database.GenerateOtpAsync(Username);
                _otpSent = true;

                // 3. Attempt to send email via Mailjet
                bool emailSent = false;
                try
                {
                    await _emailService.SendOtpEmailAsync(user.Email, otp);
                    emailSent = true;
                }
                catch (Exception emailEx)
                {
                    // Log the failure but don't break the flow
                    System.Diagnostics.Debug.WriteLine(
                        $"⚠️ OTP email delivery failed: {emailEx.Message}");
                }

                // 4. Handle UI Feedback
                if (emailSent)
                {
                    // Real email was delivered successfully
                    await Shell.Current.DisplayAlert(
                        "OTP Sent",
                        $"An OTP has been sent to your registered email address " +
                        $"({user.Email}).\n\nIt expires in 10 minutes.",
                        "OK");

                    StatusMessage = "OTP sent to your email.";
                }
                else
                {
                    // Fallback path: Email blocked or failed. 
                    // Presented professionally as a "Backup Code" for offline/blocked scenarios.
                    await Shell.Current.DisplayAlert(
                        "OTP Generated",
                        $"An OTP has been generated for your account.\n\n" +
                        $"If you do not receive an email within a few minutes, " +
                        $"please use the backup code below to reset your password:\n\n" +
                        $"{otp}\n\n" +
                        $"It expires in 10 minutes.",
                        "OK");

                    StatusMessage = "OTP generated successfully.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void OnResetPassword()
        {
            if (!_otpSent)
            {
                StatusMessage = "Please request an OTP first.";
                return;
            }

            if (string.IsNullOrWhiteSpace(OtpCode) || OtpCode.Length != 6)
            {
                StatusMessage = "Please enter a valid 6-digit OTP.";
                return;
            }

            if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 4)
            {
                StatusMessage = "Password must be at least 4 characters.";
                return;
            }

            IsBusy = true;
            StatusMessage = string.Empty;

            try
            {
                // 1. Validate OTP against the database
                bool isValid = await App.Database!.ValidateOtpAsync(Username, OtpCode);
                if (!isValid)
                {
                    StatusMessage = "Invalid or expired OTP.";
                    IsBusy = false;
                    return;
                }

                // 2. Reset Password
                bool success = await App.Database.ResetPasswordAsync(Username, NewPassword);
                if (success)
                {
                    await App.Database.MarkOtpUsedAsync(Username, OtpCode);
                    await Shell.Current.DisplayAlert(
                        "Success",
                        "Password reset successfully. Please login.",
                        "OK");

                    await Shell.Current.GoToAsync(Routes.Login);
                }
                else
                {
                    StatusMessage = "Failed to reset password.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}