using HRMS.Model;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace HRMS.View
{
    public partial class SetupOtpWindow : Window
    {
        private static readonly Brush InfoBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#35516E"));
        private static readonly Brush SuccessBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#236B43"));
        private static readonly Brush ErrorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C33A33"));
        private static readonly Brush InfoPanelBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EFF6FF"));
        private static readonly Brush InfoPanelBorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D6E8FF"));
        private static readonly Brush SuccessPanelBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EEF9F1"));
        private static readonly Brush SuccessPanelBorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CFEBD8"));
        private static readonly Brush ErrorPanelBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF1F0"));
        private static readonly Brush ErrorPanelBorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F6C9C6"));

        private readonly SetupOtpChallengeService _challengeService;
        private bool _isBusy;

        public SetupOtpWindow(SetupOtpChallengeService challengeService)
        {
            InitializeComponent();
            _challengeService = challengeService;
            ConfigPathText.Text = _challengeService.ConfigPath;
            EmailAddressTextBox.Text = _challengeService.DefaultRecipientEmail;
            Loaded += SetupOtpWindow_OnLoaded;
        }

        private void SetupOtpWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshSenderConfigurationState();
            UpdateDestinationSummary();
            SetStatus("Enter your email address, then click Send OTP.", InfoBrush, InfoPanelBrush, InfoPanelBorderBrush);
            EmailAddressTextBox.Focus();
            EmailAddressTextBox.SelectAll();
        }

        private void EmailAddressTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateDestinationSummary();
        }

        private void EmailAddressTextBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SendOtpButton_OnClick(sender, e);
                e.Handled = true;
            }
        }

        private async void SendOtpButton_OnClick(object sender, RoutedEventArgs e)
        {
            await SendOtpAsync().ConfigureAwait(true);
        }

        private async Task SendOtpAsync()
        {
            if (_isBusy)
            {
                return;
            }

            try
            {
                _isBusy = true;
                ToggleInputs(false);
                SetStatus("Sending OTP to your email...", InfoBrush, InfoPanelBrush, InfoPanelBorderBrush);

                var result = await _challengeService.SendCodeAsync(EmailAddressTextBox.Text).ConfigureAwait(true);
                RefreshSenderConfigurationState();
                UpdateDestinationSummary();
                SetStatus(
                    GetStatusMessage(result.Message),
                    result.Success ? SuccessBrush : ErrorBrush,
                    result.Success ? SuccessPanelBrush : ErrorPanelBrush,
                    result.Success ? SuccessPanelBorderBrush : ErrorPanelBorderBrush);

                if (result.Success)
                {
                    OtpCodeTextBox.Focus();
                    OtpCodeTextBox.SelectAll();
                }
                else if (ConfigHelpPanel.Visibility == Visibility.Visible)
                {
                    OpenConfigButton.Focus();
                }
                else
                {
                    EmailAddressTextBox.Focus();
                    EmailAddressTextBox.SelectAll();
                }
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, ErrorBrush, ErrorPanelBrush, ErrorPanelBorderBrush);
            }
            finally
            {
                _isBusy = false;
                ToggleInputs(true);
            }
        }

        private void VerifyCurrentOtp()
        {
            var result = _challengeService.VerifyCode(OtpCodeTextBox.Text);
            SetStatus(
                result.Message,
                result.Success ? SuccessBrush : ErrorBrush,
                result.Success ? SuccessPanelBrush : ErrorPanelBrush,
                result.Success ? SuccessPanelBorderBrush : ErrorPanelBorderBrush);

            if (!result.Success)
            {
                OtpCodeTextBox.Focus();
                OtpCodeTextBox.SelectAll();
                return;
            }

            DialogResult = true;
            Close();
        }

        private void VerifyButton_OnClick(object sender, RoutedEventArgs e)
        {
            VerifyCurrentOtp();
        }

        private void CancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OpenConfigButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = _challengeService.ConfigPath;
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (!File.Exists(path))
                {
                    File.WriteAllLines(path,
                    [
                        "# Brevo OTP settings for login-page database setup protection.",
                        "# RecipientEmail is optional and only pre-fills the email field.",
                        "ApiKey=",
                        "SenderName=HRMS Security",
                        "SenderEmail=",
                        "RecipientName=HRMS Administrator",
                        "RecipientEmail=",
                        "OtpLength=6",
                        "OtpTtlMinutes=5",
                        "AccessWindowMinutes=10",
                        "ResendCooldownSeconds=45",
                        "SandboxMode=false"
                    ]);
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = true
                });

                SetStatus("Update the Brevo sender settings, save the file, then click Send OTP again.", InfoBrush, InfoPanelBrush, InfoPanelBorderBrush);
            }
            catch (Exception ex)
            {
                SetStatus($"Unable to open Brevo config: {ex.Message}", ErrorBrush, ErrorPanelBrush, ErrorPanelBorderBrush);
            }
        }

        private void OtpCodeTextBox_OnPreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }

        private void OtpCodeTextBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                VerifyCurrentOtp();
                e.Handled = true;
            }
        }

        private void ToggleInputs(bool isEnabled)
        {
            EmailAddressTextBox.IsEnabled = isEnabled;
            SendOtpButton.IsEnabled = isEnabled;
            OtpCodeTextBox.IsEnabled = isEnabled;
            VerifyButton.IsEnabled = isEnabled;
            CancelButton.IsEnabled = isEnabled;
            OpenConfigButton.IsEnabled = isEnabled;
        }

        private void RefreshSenderConfigurationState()
        {
            var hasSenderConfiguration = BrevoOtpConfig.TryValidate(BrevoOtpConfig.GetSettings(), out var message);
            ConfigHelpPanel.Visibility = hasSenderConfiguration ? Visibility.Collapsed : Visibility.Visible;
            ConfigMessageText.Text = hasSenderConfiguration
                ? string.Empty
                : message + " Configure the sender details once, then use your email in the field above.";
        }

        private void UpdateDestinationSummary()
        {
            var email = EmailAddressTextBox.Text?.Trim() ?? string.Empty;
            DestinationSummaryText.Text = string.IsNullOrWhiteSpace(email)
                ? "No email entered yet."
                : $"OTP will be delivered to: {email}";
        }

        private void SetStatus(string text, Brush foreground, Brush background, Brush borderBrush)
        {
            StatusText.Text = text;
            StatusText.Foreground = foreground;
            StatusPanel.Background = background;
            StatusPanel.BorderBrush = borderBrush;
        }

        private string GetStatusMessage(string message)
        {
            if (ConfigHelpPanel.Visibility != Visibility.Visible)
            {
                return message;
            }

            if (message.Contains("Missing Brevo API key", StringComparison.OrdinalIgnoreCase))
            {
                return "Brevo API key is missing. Use Open Config.";
            }

            if (message.Contains("Missing Brevo sender email", StringComparison.OrdinalIgnoreCase))
            {
                return "Brevo sender email is missing. Use Open Config.";
            }

            return message;
        }
    }
}
