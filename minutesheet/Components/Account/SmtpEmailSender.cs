using System.Net.Http.Headers;
using System.Net.Http.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MimeKit;
using minutesheet.Data;

namespace minutesheet.Components.Account
{
    public sealed class EmailSettings
    {
        // Brevo HTTP API key (starts with "xkeysib-"). Preferred: works over HTTPS/443,
        // which — unlike SMTP ports — is rarely blocked by ISPs/cloud networks.
        public string ApiKey { get; set; } = "";

        // SMTP settings (fallback, used only when ApiKey is empty).
        public string Host { get; set; } = "";
        public int Port { get; set; } = 587;
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public bool EnableSsl { get; set; } = true;

        // Sender identity (must be a verified sender in Brevo).
        public string From { get; set; } = "";
        public string FromName { get; set; } = "Minute Sheet";
    }

    // Sends real email. Prefers the Brevo HTTP API (HTTPS); falls back to SMTP (MailKit)
    // when no API key is configured. Also satisfies IEmailSender<ApplicationUser> so
    // Identity's password-reset flow keeps working.
    public sealed class SmtpEmailSender(
        IOptions<EmailSettings> options,
        IHttpClientFactory httpClientFactory,
        ILogger<SmtpEmailSender> logger)
        : IEmailSender<ApplicationUser>
    {
        private readonly EmailSettings _settings = options.Value;

        // Returns true if the OTP mail was sent. Never throws, so a mail failure
        // can't break signup — the user still reaches the verify page and can resend.
        public async Task<bool> SendOtpAsync(string email, string code)
        {
            var body = $"""
                <div style="font-family:'Poppins',Arial,sans-serif;max-width:480px;margin:auto;padding:24px;color:#1a1a1a">
                    <h2 style="color:#000066;margin:0 0 12px">Verify your email</h2>
                    <p>Use the verification code below to finish creating your Minute Sheet account.</p>
                    <p style="font-size:32px;font-weight:700;letter-spacing:8px;color:#000066;margin:20px 0">{code}</p>
                    <p style="color:#666;font-size:13px">This code expires shortly. If you didn't request it, you can ignore this email.</p>
                </div>
                """;
            try
            {
                await SendAsync(email, "Your Minute Sheet verification code", body);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send OTP email to {Email}.", email);
                return false;
            }
        }

        // Generic workflow notification (approval requested / review left / review resolved).
        // Never throws so a mail failure can't break the workflow action that triggered it.
        public async Task<bool> SendSheetNotificationAsync(string email, string subject, string message, string link,
            byte[]? attachmentBytes = null, string? attachmentName = null)
        {
            var body = $"""
                <div style="font-family:'Poppins',Arial,sans-serif;max-width:480px;margin:auto;padding:24px;color:#1a1a1a">
                    <h2 style="color:#000066;margin:0 0 12px">Minute Sheet</h2>
                    <p>{message}</p>
                    <p style="margin:24px 0">
                        <a href="{link}" style="background:#000066;color:#fff;text-decoration:none;padding:12px 20px;border-radius:8px;display:inline-block">Open the minute sheet</a>
                    </p>
                    {(attachmentBytes is null ? "" : "<p style=\"color:#666;font-size:13px\">A PDF copy of the minute sheet is attached to this email.</p>")}
                    <p style="color:#666;font-size:13px">If the button doesn't work, paste this link into your browser:<br>{link}</p>
                </div>
                """;
            try
            {
                await SendAsync(email, subject, body, attachmentBytes, attachmentName);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send sheet notification to {Email}.", email);
                return false;
            }
        }

        public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
            SendAsync(email, "Confirm your email",
                $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");

        public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
            SendAsync(email, "Reset your password",
                $"Please reset your password by <a href='{resetLink}'>clicking here</a>.");

        public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
            SendAsync(email, "Reset your password",
                $"Please reset your password using the following code: {resetCode}");

        private Task SendAsync(string to, string subject, string htmlBody, byte[]? attachmentBytes = null, string? attachmentName = null)
        {
            if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                return SendViaBrevoApiAsync(to, subject, htmlBody, attachmentBytes, attachmentName);
            }
            if (!string.IsNullOrWhiteSpace(_settings.Host))
            {
                return SendViaSmtpAsync(to, subject, htmlBody, attachmentBytes, attachmentName);
            }

            logger.LogWarning(
                "No email transport configured (EmailSettings:ApiKey / :Host both empty). Email to {To} ('{Subject}') was NOT sent.",
                to, subject);
            return Task.CompletedTask;
        }

        private async Task SendViaBrevoApiAsync(string to, string subject, string htmlBody,
            byte[]? attachmentBytes = null, string? attachmentName = null)
        {
            var from = string.IsNullOrWhiteSpace(_settings.From) ? _settings.User : _settings.From;
            var payload = new Dictionary<string, object?>
            {
                ["sender"] = new { email = from, name = _settings.FromName },
                ["to"] = new[] { new { email = to } },
                ["subject"] = subject,
                ["htmlContent"] = htmlBody
            };

            if (attachmentBytes is not null && attachmentBytes.Length > 0)
            {
                payload["attachment"] = new[]
                {
                    new
                    {
                        content = Convert.ToBase64String(attachmentBytes),
                        name = string.IsNullOrWhiteSpace(attachmentName) ? "minute-sheet.pdf" : attachmentName
                    }
                };
            }

            using var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(20);
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            request.Headers.Add("api-key", _settings.ApiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = JsonContent.Create(payload);

            using var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"Brevo API returned {(int)response.StatusCode} {response.StatusCode}: {detail}");
            }
        }

        private async Task SendViaSmtpAsync(string to, string subject, string htmlBody,
            byte[]? attachmentBytes = null, string? attachmentName = null)
        {
            var builder = new BodyBuilder { HtmlBody = htmlBody };
            if (attachmentBytes is not null && attachmentBytes.Length > 0)
            {
                builder.Attachments.Add(
                    string.IsNullOrWhiteSpace(attachmentName) ? "minute-sheet.pdf" : attachmentName,
                    attachmentBytes);
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, string.IsNullOrWhiteSpace(_settings.From) ? _settings.User : _settings.From));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient { Timeout = 20000 }; // 20s so a network stall can't hang the request
            var socketOptions = _settings.EnableSsl
                ? (_settings.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls)
                : SecureSocketOptions.None;

            await client.ConnectAsync(_settings.Host, _settings.Port, socketOptions);
            if (!string.IsNullOrWhiteSpace(_settings.User))
            {
                await client.AuthenticateAsync(_settings.User, _settings.Password);
            }
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}
