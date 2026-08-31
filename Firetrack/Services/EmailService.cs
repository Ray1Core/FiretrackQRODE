using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Firetrack.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Sends an OTP email to the user.
        /// </summary>
        public async Task SendOtpEmailAsync(string recipientEmail, string otpCode)
        {
            var smtpServer = _config["Email:SmtpServer"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_config["Email:SmtpPort"] ?? "587");
            var senderEmail = _config["Email:SenderEmail"] ?? "your-email@gmail.com";
            var senderPassword = _config["Email:SenderPassword"] ?? "your-app-password";

            using var client = new SmtpClient(smtpServer, smtpPort)
            {
                Credentials = new NetworkCredential(senderEmail, senderPassword),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, "Fire Track System"),
                Subject = "Your OTP Code for Password Reset",
                Body = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2>Password Reset Request</h2>
                        <p>Hello,</p>
                        <p>You requested to reset your password. Use the following One-Time Password (OTP) to proceed:</p>
                        <h1 style='background: #f4f4f4; padding: 15px; text-align: center; font-size: 28px; letter-spacing: 4px;'>
                            {otpCode}
                        </h1>
                        <p><strong>This OTP is valid for 10 minutes.</strong></p>
                        <p>If you did not request this, please ignore this email.</p>
                        <hr />
                        <p style='color: gray; font-size: 12px;'>Fire Track – BFP Cebu City Station</p>
                    </body>
                    </html>",
                IsBodyHtml = true
            };

            mailMessage.To.Add(recipientEmail);

            try
            {
                await client.SendMailAsync(mailMessage);
                System.Diagnostics.Debug.WriteLine($"✅ OTP email sent to {recipientEmail}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Failed to send OTP email: {ex.Message}");
                throw; // rethrow so the ViewModel can handle it
            }
        }
    }
}