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
                var user = await App.Database!.GetUserByUsernameAsync(Username);
                if (user == null)
                {
                    StatusMessage = "Username not found.";
                    IsBusy = false;
                    return;
                }

                string otp = await App.Database.GenerateOtpAsync(Username);
                _otpSent = true;

                // ============================================================
                // Try to send OTP via email first.
                // If SMTP is not configured (or internet is down), fall back
                // to displaying the OTP directly in an alert dialog.
                // ============================================================
                bool emailSent = false;
                try
                {
                    await _emailService.SendOtpEmailAsync(user.Email, otp);
                    emailSent = true;
                }
                catch (Exception emailEx)
                {
                    // Log the SMTP failure but don't break the flow
                    System.Diagnostics.Debug.WriteLine(
                        $"⚠️ OTP email delivery failed: {emailEx.Message}");
                }

                if (emailSent)
                {
                    // Success path — real email was delivered
                    await Shell.Current.DisplayAlert(
                        "OTP Sent",
                        $"An OTP has been sent to your registered email address " +
                        $"({user.Email}).\n\nIt expires in 10 minutes.",
                        "OK");

                    StatusMessage = "OTP sent to your email.";
                }
                else
                {
                    // Fallback path — SMTP unavailable, show OTP on screen
                    await Shell.Current.DisplayAlert(
                        "OTP (Demo Mode)",
                        $"Email delivery is not configured or failed.\n\n" +
                        $"Your OTP is:\n\n{otp}\n\n" +
                        $"It expires in 10 minutes.",
                        "OK");

                    StatusMessage = "OTP displayed on screen (demo mode).";
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
                bool isValid = await App.Database!.ValidateOtpAsync(Username, OtpCode);
                if (!isValid)
                {
                    StatusMessage = "Invalid or expired OTP.";
                    IsBusy = false;
                    return;
                }

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