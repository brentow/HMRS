using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public sealed record BrevoEmailSendResult(string MessageId);

    public sealed class BrevoTransactionalEmailService
    {
        private static readonly HttpClient HttpClient = new()
        {
            BaseAddress = new Uri("https://api.brevo.com/"),
            Timeout = TimeSpan.FromSeconds(30)
        };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public async Task<BrevoEmailSendResult> SendSetupOtpAsync(BrevoOtpSettings settings, string recipientEmail, string otpCode, CancellationToken cancellationToken = default)
        {
            var recipientName = ResolveRecipientName(recipientEmail, settings.RecipientName, settings.RecipientEmail);
            var request = new SendTransactionalEmailRequest
            {
                Sender = new EmailParty
                {
                    Name = settings.SenderName,
                    Email = settings.SenderEmail
                },
                To =
                [
                    new EmailParty
                    {
                        Name = recipientName,
                        Email = recipientEmail
                    }
                ],
                Subject = "ePRIME OTP Code - Access Database Setup",
                TextContent = BuildTextContent(otpCode, settings.OtpTtlMinutes, recipientEmail),
                HtmlContent = BuildHtmlContent(otpCode, settings.OtpTtlMinutes, recipientEmail),
                Headers = settings.SandboxMode
                    ? new()
                    {
                        ["X-Sib-Sandbox"] = "drop"
                    }
                    : null
            };

            using var message = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email");
            message.Headers.Add("api-key", settings.ApiKey);
            message.Headers.Accept.ParseAdd("application/json");
            message.Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json");

            using var response = await HttpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var detail = string.IsNullOrWhiteSpace(responseContent)
                    ? response.ReasonPhrase ?? "Unknown Brevo error."
                    : responseContent.Trim();
                throw new InvalidOperationException($"Brevo OTP send failed ({(int)response.StatusCode}): {detail}");
            }

            var parsed = JsonSerializer.Deserialize<SendTransactionalEmailResponse>(responseContent, JsonOptions);
            return new BrevoEmailSendResult(parsed?.MessageId ?? string.Empty);
        }

        private static string BuildTextContent(string otpCode, int ttlMinutes, string recipientEmail)
        {
            return
                "ePRIME+ OTP Code - Access Database Setup" + Environment.NewLine + Environment.NewLine +
                "Your one-time password is below." + Environment.NewLine + Environment.NewLine +
                "Action: Access database setup" + Environment.NewLine +
                "Scope: Login-page connection settings for HRMS, GGMS, and CRS." + Environment.NewLine +
                $"Recipient: {recipientEmail}" + Environment.NewLine + Environment.NewLine +
                $"OTP Code: {otpCode}" + Environment.NewLine +
                $"Expires In: {ttlMinutes} minute(s)" + Environment.NewLine + Environment.NewLine +
                "If you did not request this action, you can safely ignore this message.";
        }

        private static string BuildHtmlContent(string otpCode, int ttlMinutes, string recipientEmail)
        {
            var encodedOtp = WebUtility.HtmlEncode(otpCode);
            var encodedRecipient = WebUtility.HtmlEncode(recipientEmail);

            return
                "<html><body style=\"margin:0;padding:18px;background:#F8FAFD;font-family:Segoe UI,Arial,sans-serif;color:#2A2A2A;line-height:1.45;\">" +
                "<div style=\"max-width:560px;margin:0 auto;background:#FFFFFF;border:1px solid #D9E3F0;border-radius:10px;padding:16px;\">" +
                "<div style=\"font-size:18px;font-weight:700;color:#173B5E;margin-bottom:10px;\">ePRIME OTP Code - Access Database Setup</div>" +
                "<p style=\"margin:0 0 10px 0;\">Your one-time password is below.</p>" +
                "<p style=\"margin:0 0 12px 0;\"><strong>Action:</strong> Access database setup<br/>" +
                "<strong>Scope:</strong> Login-page connection settings for HRMS, GGMS, and CRS.</p>" +
                "<div style=\"margin:10px 0 12px 0;padding:12px;border:1px solid #C9D8EA;background:#EEF5FF;border-radius:8px;text-align:center;\">" +
                "<div style=\"font-size:11px;font-weight:600;color:#4B627B;letter-spacing:0.6px;\">ONE-TIME PASSWORD</div>" +
                $"<div style=\"font-size:34px;font-weight:800;letter-spacing:7px;color:#0D4E8C;margin-top:4px;\">{encodedOtp}</div>" +
                $"<div style=\"font-size:12px;color:#4B627B;margin-top:6px;\">Expires in {ttlMinutes} minute(s)</div>" +
                "</div>" +
                $"<p style=\"margin:0 0 10px 0;\"><strong>Recipient:</strong> {encodedRecipient}</p>" +
                "<p style=\"margin:0;\">If you did not request this action, you can safely ignore this message.</p>" +
                "</div>" +
                "</body></html>";
        }

        private static string ResolveRecipientName(string recipientEmail, string configuredName, string configuredEmail)
        {
            if (!string.IsNullOrWhiteSpace(configuredName) &&
                !string.IsNullOrWhiteSpace(configuredEmail) &&
                recipientEmail.Equals(configuredEmail.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return configuredName.Trim();
            }

            var atIndex = recipientEmail.IndexOf('@');
            return atIndex > 0 ? recipientEmail[..atIndex] : recipientEmail;
        }

        private sealed class SendTransactionalEmailRequest
        {
            public EmailParty Sender { get; set; } = new();
            public List<EmailParty> To { get; set; } = [];
            public string Subject { get; set; } = string.Empty;
            public string TextContent { get; set; } = string.Empty;
            public string HtmlContent { get; set; } = string.Empty;
            public Dictionary<string, string>? Headers { get; set; }
        }

        private sealed class EmailParty
        {
            public string Name { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
        }

        private sealed class SendTransactionalEmailResponse
        {
            public string MessageId { get; set; } = string.Empty;
        }
    }
}
