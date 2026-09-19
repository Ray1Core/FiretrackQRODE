using System;
using System.Threading.Tasks;
using Mailjet.Client;
using Mailjet.Client.Resources;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq; // Ensure you have the Newtonsoft.Json package

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
        /// Sends an OTP email to the user using Mailjet.
        /// </summary>
        public async Task SendOtpEmailAsync(string recipientEmail, string otpCode)
        {
            var apiKey = _config["Email:MailjetApiKey"];
            var secretKey = _config["Email:MailjetSecretKey"];
            var senderEmail = _config["Email:SenderEmail"] ?? "noreply@firetrack.gov";

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(secretKey) || apiKey == "YOUR_API_KEY_HERE")
            {
                throw new InvalidOperationException("Mailjet API keys are not configured in appsettings.json.");
            }

            // The official client class is MailjetClient from the Mailjet.Api package
            MailjetClient client = new MailjetClient(apiKey, secretKey);

            MailjetRequest request = new MailjetRequest
            {
                Resource = Send.Resource,
            }
            .Property(Send.FromEmail, senderEmail)
            .Property(Send.FromName, "Fire Track System")
            .Property(Send.Subject, "Your OTP Code for Password Reset")
            .Property(Send.HtmlPart, $@"
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
                </html>")
            .Property(Send.TextPart, $"Your OTP code is: {otpCode}")
            .Property(Send.Recipients, new JArray {
                new JObject {
                    { "Email", recipientEmail }
                }
            });

            MailjetResponse response = await client.PostAsync(request);

            if (response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"✅ OTP email sent to {recipientEmail} via Mailjet");
            }
            else
            {
                var errorMessage = $"❌ Failed to send OTP email via Mailjet: {response.StatusCode} - {response.GetData()}";
                System.Diagnostics.Debug.WriteLine(errorMessage);
                throw new Exception(errorMessage);
            }
        }
    }
}