using Firetrack.Services;
using Firetrack.ViewModels;
using Microsoft.Maui.Controls;

namespace Firetrack.Views;

public partial class ForgotPasswordPage : ContentPage
{
    public ForgotPasswordPage()
    {
        InitializeComponent();

        // Get the EmailService from the DI container
        var emailService = MauiProgram.Services.GetRequiredService<EmailService>();
        BindingContext = new ForgotPasswordViewModel(emailService);
    }
}