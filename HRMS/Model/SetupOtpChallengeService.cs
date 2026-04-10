using System;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public sealed record OtpOperationResult(bool Success, string Message);

    public sealed class SetupOtpChallengeService
    {
        private const int MaxVerificationAttempts = 5;

        private readonly BrevoTransactionalEmailService _emailService;
        private BrevoOtpSettings _activeSettings;

        private string? _activeOtpHash;
        private string? _activeRecipientEmail;
        private DateTimeOffset _otpExpiresAtUtc;
        private DateTimeOffset _resendAvailableAtUtc;
        private DateTimeOffset _setupAccessExpiresAtUtc;
        private int _failedVerificationAttempts;

        public SetupOtpChallengeService()
            : this(BrevoOtpConfig.GetSettings(), new BrevoTransactionalEmailService())
        {
        }

        public SetupOtpChallengeService(BrevoOtpSettings settings, BrevoTransactionalEmailService emailService)
        {
            _activeSettings = settings;
            _emailService = emailService;
        }

        public string RecipientHint =>
            !string.IsNullOrWhiteSpace(_activeRecipientEmail)
                ? MaskEmail(_activeRecipientEmail)
                : !string.IsNullOrWhiteSpace(_activeSettings.RecipientEmail)
                    ? MaskEmail(_activeSettings.RecipientEmail)
                    : "Enter your email to receive the OTP";

        public string ConfigPath => BrevoOtpConfig.GetTextSettingsFilePath();
        public string DefaultRecipientEmail => _activeSettings.RecipientEmail;

        public bool HasVerifiedSetupAccess => _setupAccessExpiresAtUtc > DateTimeOffset.UtcNow;

        public async Task<OtpOperationResult> SendCodeAsync(string? recipientEmail, CancellationToken cancellationToken = default)
        {
            var settings = BrevoOtpConfig.GetSettings();
            if (!BrevoOtpConfig.TryValidate(settings, out var validationMessage))
            {
                return new OtpOperationResult(false, validationMessage);
            }

            var normalizedRecipient = NormalizeRecipientEmail(recipientEmail, settings.RecipientEmail);
            if (string.IsNullOrWhiteSpace(normalizedRecipient))
            {
                return new OtpOperationResult(false, "Enter the email address that should receive the OTP.");
            }

            if (!IsValidEmail(normalizedRecipient))
            {
                return new OtpOperationResult(false, "Enter a valid email address before sending the OTP.");
            }

            var now = DateTimeOffset.UtcNow;
            if (_resendAvailableAtUtc > now)
            {
                var secondsLeft = Math.Max(1, (int)Math.Ceiling((_resendAvailableAtUtc - now).TotalSeconds));
                return new OtpOperationResult(false, $"Please wait {secondsLeft} second(s) before requesting another OTP.");
            }

            _activeSettings = settings;
            _activeRecipientEmail = normalizedRecipient;

            var otpCode = GenerateNumericCode(settings.OtpLength);
            _activeOtpHash = HashOtp(otpCode);
            _otpExpiresAtUtc = now.AddMinutes(settings.OtpTtlMinutes);
            _resendAvailableAtUtc = now.AddSeconds(settings.ResendCooldownSeconds);
            _failedVerificationAttempts = 0;

            await _emailService.SendSetupOtpAsync(settings, normalizedRecipient, otpCode, cancellationToken).ConfigureAwait(false);
            return new OtpOperationResult(true, $"OTP sent to {RecipientHint}. It expires in {settings.OtpTtlMinutes} minute(s).");
        }

        public OtpOperationResult VerifyCode(string? input)
        {
            if (!BrevoOtpConfig.TryValidate(_activeSettings, out var validationMessage))
            {
                return new OtpOperationResult(false, validationMessage);
            }

            if (string.IsNullOrWhiteSpace(_activeOtpHash))
            {
                return new OtpOperationResult(false, "No OTP is active. Send a new OTP first.");
            }

            var now = DateTimeOffset.UtcNow;
            if (_otpExpiresAtUtc <= now)
            {
                ClearChallenge();
                return new OtpOperationResult(false, "The OTP expired. Send a new code.");
            }

            var normalized = input?.Trim() ?? string.Empty;
            if (normalized.Length != _activeSettings.OtpLength)
            {
                return new OtpOperationResult(false, $"Enter the {_activeSettings.OtpLength}-digit OTP.");
            }

            if (!MatchesHash(_activeOtpHash, normalized))
            {
                _failedVerificationAttempts++;
                if (_failedVerificationAttempts >= MaxVerificationAttempts)
                {
                    ClearChallenge();
                    return new OtpOperationResult(false, "Too many incorrect OTP attempts. Request a new code.");
                }

                var attemptsLeft = MaxVerificationAttempts - _failedVerificationAttempts;
                return new OtpOperationResult(false, $"Incorrect OTP. {attemptsLeft} attempt(s) remaining.");
            }

            _setupAccessExpiresAtUtc = now.AddMinutes(_activeSettings.AccessWindowMinutes);
            ClearChallenge();
            return new OtpOperationResult(true, $"OTP verified. Database setup is unlocked for {_activeSettings.AccessWindowMinutes} minute(s).");
        }

        private void ClearChallenge()
        {
            _activeOtpHash = null;
            _otpExpiresAtUtc = default;
            _resendAvailableAtUtc = default;
            _failedVerificationAttempts = 0;
        }

        private static string GenerateNumericCode(int length)
        {
            var builder = new StringBuilder(length);
            for (var index = 0; index < length; index++)
            {
                builder.Append(RandomNumberGenerator.GetInt32(0, 10));
            }

            return builder.ToString();
        }

        private static string HashOtp(string otp)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(otp));
            return Convert.ToHexString(bytes);
        }

        private static bool MatchesHash(string expectedHash, string candidateOtp)
        {
            var expectedBytes = Convert.FromHexString(expectedHash);
            var candidateBytes = SHA256.HashData(Encoding.UTF8.GetBytes(candidateOtp));
            return CryptographicOperations.FixedTimeEquals(expectedBytes, candidateBytes);
        }

        private static string MaskEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return "Enter your email to receive the OTP";
            }

            var trimmed = email.Trim();
            var atIndex = trimmed.IndexOf('@');
            if (atIndex <= 1)
            {
                return trimmed;
            }

            var localPart = trimmed[..atIndex];
            var domain = trimmed[atIndex..];
            var visiblePrefix = localPart[..Math.Min(2, localPart.Length)];
            return visiblePrefix + new string('*', Math.Max(1, localPart.Length - visiblePrefix.Length)) + domain;
        }

        private static string NormalizeRecipientEmail(string? recipientEmail, string? fallbackEmail)
        {
            var candidate = string.IsNullOrWhiteSpace(recipientEmail) ? fallbackEmail : recipientEmail;
            return candidate?.Trim() ?? string.Empty;
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var parsed = new MailAddress(email);
                return string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
