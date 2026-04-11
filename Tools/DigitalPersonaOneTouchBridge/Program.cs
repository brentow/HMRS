using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using DPFP;
using DPFP.Capture;
using DPFP.Gui;
using DPFP.Gui.Enrollment;
using DPFP.Processing;
using DPFP.Verification;

namespace DigitalPersonaOneTouchBridge
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length >= 1 && string.Equals((args[0] ?? string.Empty).Trim(), "diagnostic-ui", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new DiagnosticForm());
                return 0;
            }

            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: DigitalPersonaOneTouchBridge <capture-template|identify-template> <output-file> [manifest-file] [timeout-ms]");
                Console.Error.WriteLine("   or: DigitalPersonaOneTouchBridge diagnostic-ui");
                return 2;
            }

            var command = (args[0] ?? string.Empty).Trim();
            var outputPath = Path.GetFullPath(args[1]);
            var timeoutMs = ParseTimeout(args);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? AppContext.BaseDirectory);

                switch (command.ToLowerInvariant())
                {
                    case "capture-template":
                        Application.Run(new CaptureTemplateForm(outputPath, timeoutMs));
                        return 0;

                    case "identify-template":
                        if (args.Length < 3)
                        {
                            Console.Error.WriteLine("identify-template requires a manifest file path.");
                            return 2;
                        }

                        var manifestPath = Path.GetFullPath(args[2]);
                        Application.Run(new IdentifyTemplateForm(outputPath, manifestPath, timeoutMs));
                        return 0;

                    default:
                        Console.Error.WriteLine($"Unknown command: {command}");
                        return 2;
                }
            }
            catch (Exception ex)
            {
                WriteSharedOutput(outputPath, new[]
                {
                    "status=error",
                    $"message={Escape(ex.Message)}"
                });
                return 1;
            }
        }

        private static int ParseTimeout(string[] args)
        {
            var candidate = args.Length >= 4 ? args[3] : (args.Length >= 3 && string.Equals(args[0], "capture-template", StringComparison.OrdinalIgnoreCase) ? args[2] : null);
            return int.TryParse(candidate, out var timeoutMs) && timeoutMs > 0 ? timeoutMs : 15000;
        }

        internal static string Escape(string value)
            => (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();

        internal static void WriteSharedOutput(string outputPath, IEnumerable<string> lines)
        {
            using var stream = new FileStream(
                outputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.ReadWrite);
            using var writer = new StreamWriter(stream);
            foreach (var line in lines)
            {
                writer.WriteLine(line ?? string.Empty);
            }
        }
    }

    internal sealed class DiagnosticForm : Form
    {
        private readonly EnrollmentControl _enrollmentControl;
        private readonly ListBox _eventList;
        private readonly Label _statusLabel;

        public DiagnosticForm()
        {
            Text = "DigitalPersona Reader Diagnostic";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(560, 520);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            TopMost = true;

            var titleLabel = new Label
            {
                AutoSize = true,
                Location = new Point(16, 14),
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Text = "Fingerprint Reader Test"
            };

            var infoLabel = new Label
            {
                AutoSize = false,
                Location = new Point(16, 42),
                Size = new Size(520, 36),
                Font = new Font("Segoe UI", 9F),
                Text = "Place one finger on the reader. This window will log touch, quality, and completion events directly from the DigitalPersona SDK."
            };

            _statusLabel = new Label
            {
                AutoSize = false,
                Location = new Point(16, 84),
                Size = new Size(520, 28),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.DarkGreen,
                Text = "Waiting for reader events..."
            };

            _enrollmentControl = new EnrollmentControl
            {
                Name = "DiagnosticEnrollmentControl",
                Location = new Point(16, 120),
                Size = new Size(520, 300),
                EnrolledFingerMask = 0,
                MaxEnrollFingerCount = 1,
                ReaderSerialNumber = "00000000-0000-0000-0000-000000000000"
            };

            _eventList = new ListBox
            {
                Location = new Point(16, 430),
                Size = new Size(520, 72),
                HorizontalScrollbar = true
            };

            _enrollmentControl.OnStartEnroll += EnrollmentControl_OnStartEnroll;
            _enrollmentControl.OnFingerTouch += EnrollmentControl_OnFingerTouch;
            _enrollmentControl.OnFingerRemove += EnrollmentControl_OnFingerRemove;
            _enrollmentControl.OnSampleQuality += EnrollmentControl_OnSampleQuality;
            _enrollmentControl.OnReaderConnect += EnrollmentControl_OnReaderConnect;
            _enrollmentControl.OnReaderDisconnect += EnrollmentControl_OnReaderDisconnect;
            _enrollmentControl.OnCancelEnroll += EnrollmentControl_OnCancelEnroll;
            _enrollmentControl.OnComplete += EnrollmentControl_OnComplete;
            _enrollmentControl.OnDelete += EnrollmentControl_OnDelete;
            _enrollmentControl.OnEnroll += EnrollmentControl_OnEnroll;

            Controls.Add(titleLabel);
            Controls.Add(infoLabel);
            Controls.Add(_statusLabel);
            Controls.Add(_enrollmentControl);
            Controls.Add(_eventList);

            Load += (_, _) => LogEvent("Diagnostic form loaded.");
        }

        private void LogEvent(string message)
        {
            var stamped = $"{DateTime.Now:HH:mm:ss} {message}";
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => LogEvent(message)));
                return;
            }

            _statusLabel.Text = message;
            _eventList.Items.Insert(0, stamped);
        }

        private void EnrollmentControl_OnStartEnroll(object control, string reader, int finger)
            => LogEvent($"OnStartEnroll: {reader}, finger {finger}");

        private void EnrollmentControl_OnFingerTouch(object control, string reader, int finger)
            => LogEvent($"OnFingerTouch: {reader}, finger {finger}");

        private void EnrollmentControl_OnFingerRemove(object control, string reader, int finger)
            => LogEvent($"OnFingerRemove: {reader}, finger {finger}");

        private void EnrollmentControl_OnSampleQuality(object control, string reader, int finger, CaptureFeedback quality)
            => LogEvent($"OnSampleQuality: {reader}, finger {finger}, {quality}");

        private void EnrollmentControl_OnReaderConnect(object control, string reader, int finger)
            => LogEvent($"OnReaderConnect: {reader}, finger {finger}");

        private void EnrollmentControl_OnReaderDisconnect(object control, string reader, int finger)
            => LogEvent($"OnReaderDisconnect: {reader}, finger {finger}");

        private void EnrollmentControl_OnCancelEnroll(object control, string reader, int finger)
            => LogEvent($"OnCancelEnroll: {reader}, finger {finger}");

        private void EnrollmentControl_OnComplete(object control, string reader, int finger)
            => LogEvent($"OnComplete: {reader}, finger {finger}");

        private void EnrollmentControl_OnDelete(object control, int finger, ref EventHandlerStatus status)
        {
            status = EventHandlerStatus.Success;
            LogEvent($"OnDelete: finger {finger}");
        }

        private void EnrollmentControl_OnEnroll(object control, int finger, Template template, ref EventHandlerStatus status)
        {
            status = EventHandlerStatus.Success;
            LogEvent($"OnEnroll: finger {finger}, template bytes ready");
        }
    }

    internal sealed class EnrollmentControlCaptureForm : Form
    {
        private readonly string _outputPath;
        private readonly System.Windows.Forms.Timer _timeoutTimer;
        private readonly EnrollmentControl _enrollmentControl;
        private readonly Label _statusLabel;
        private readonly Label _instructionLabel;
        private bool _completed;
        private string _readerSerialNumber = string.Empty;

        public EnrollmentControlCaptureForm(string outputPath, int timeoutMs)
        {
            _outputPath = outputPath;
            Text = "HRMS Fingerprint Capture";
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(560, 460);
            MinimumSize = new Size(560, 460);
            MaximizeBox = false;
            MinimizeBox = false;
            TopMost = true;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            WindowState = FormWindowState.Normal;
            BackColor = Color.White;

            _timeoutTimer = new System.Windows.Forms.Timer { Interval = Math.Max(1000, timeoutMs) };
            _timeoutTimer.Tick += (_, _) => CompleteError("Fingerprint capture timed out.");

            var titleLabel = new Label
            {
                AutoSize = true,
                Location = new Point(16, 16),
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Text = "Capture Fingerprint"
            };

            _instructionLabel = new Label
            {
                AutoSize = false,
                Location = new Point(16, 48),
                Size = new Size(520, 44),
                Font = new Font("Segoe UI", 9.5F),
                Text = "Place one finger flat on the scanner and hold it steady until capture completes."
            };

            _statusLabel = new Label
            {
                AutoSize = false,
                Location = new Point(16, 94),
                Size = new Size(520, 26),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(24, 119, 242),
                Text = "Waiting for one clear fingerprint scan..."
            };

            _enrollmentControl = new EnrollmentControl
            {
                Name = "EnrollmentControl",
                Location = new Point(16, 128),
                Size = new Size(520, 280),
                EnrolledFingerMask = 0,
                MaxEnrollFingerCount = 1,
                ReaderSerialNumber = "00000000-0000-0000-0000-000000000000"
            };

            _enrollmentControl.OnStartEnroll += EnrollmentControl_OnStartEnroll;
            _enrollmentControl.OnFingerTouch += EnrollmentControl_OnFingerTouch;
            _enrollmentControl.OnFingerRemove += EnrollmentControl_OnFingerRemove;
            _enrollmentControl.OnSampleQuality += EnrollmentControl_OnSampleQuality;
            _enrollmentControl.OnReaderConnect += EnrollmentControl_OnReaderConnect;
            _enrollmentControl.OnReaderDisconnect += EnrollmentControl_OnReaderDisconnect;
            _enrollmentControl.OnCancelEnroll += EnrollmentControl_OnCancelEnroll;
            _enrollmentControl.OnComplete += EnrollmentControl_OnComplete;
            _enrollmentControl.OnDelete += EnrollmentControl_OnDelete;
            _enrollmentControl.OnEnroll += EnrollmentControl_OnEnroll;

            Controls.Add(titleLabel);
            Controls.Add(_instructionLabel);
            Controls.Add(_statusLabel);
            Controls.Add(_enrollmentControl);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            WriteProgress("Waiting for one clear fingerprint scan...", 0);
            _timeoutTimer.Start();
        }

        private void EnrollmentControl_OnStartEnroll(object Control, string ReaderSerialNumber, int Finger)
        {
            _readerSerialNumber = ReaderSerialNumber ?? _readerSerialNumber;
            RefreshTimeout();
            UpdateWindowStatus("Reader is ready. Place the finger on the scanner.");
            WriteProgress("Reader is ready. Place the finger on the scanner.", 0);
        }

        private void EnrollmentControl_OnFingerTouch(object Control, string ReaderSerialNumber, int Finger)
        {
            _readerSerialNumber = ReaderSerialNumber ?? _readerSerialNumber;
            RefreshTimeout();
            UpdateWindowStatus("Finger detected. Hold steady for capture.");
            WriteProgress("Finger detected. Hold steady for capture.", 0);
        }

        private void EnrollmentControl_OnFingerRemove(object Control, string ReaderSerialNumber, int Finger)
        {
            _readerSerialNumber = ReaderSerialNumber ?? _readerSerialNumber;
            RefreshTimeout();
            if (!_completed)
            {
                UpdateWindowStatus("Finger removed. If capture did not complete, place it again.");
                WriteProgress("Finger removed. If capture did not complete, place it again.", 0);
            }
        }

        private void EnrollmentControl_OnSampleQuality(object Control, string ReaderSerialNumber, int Finger, CaptureFeedback CaptureFeedback)
        {
            _readerSerialNumber = ReaderSerialNumber ?? _readerSerialNumber;
            RefreshTimeout();
            UpdateWindowStatus(
                CaptureFeedback == CaptureFeedback.Good
                    ? "Good fingerprint quality detected."
                    : "Poor fingerprint quality. Reposition the finger and try again.");
            WriteProgress(
                CaptureFeedback == CaptureFeedback.Good
                    ? "Good fingerprint quality detected."
                    : "Poor fingerprint quality. Reposition the finger and try again.",
                0);
        }

        private void EnrollmentControl_OnReaderConnect(object Control, string ReaderSerialNumber, int Finger)
        {
            _readerSerialNumber = ReaderSerialNumber ?? _readerSerialNumber;
            UpdateWindowStatus("Fingerprint reader connected and ready.");
            WriteProgress("Fingerprint reader connected and ready.", 0);
        }

        private void EnrollmentControl_OnReaderDisconnect(object Control, string ReaderSerialNumber, int Finger)
        {
            _readerSerialNumber = ReaderSerialNumber ?? _readerSerialNumber;
            if (!_completed)
            {
                UpdateWindowStatus("Fingerprint reader disconnected.");
                WriteProgress("Fingerprint reader disconnected.", 0);
            }
        }

        private void EnrollmentControl_OnCancelEnroll(object Control, string ReaderSerialNumber, int Finger)
        {
            _readerSerialNumber = ReaderSerialNumber ?? _readerSerialNumber;
            if (!_completed)
            {
                UpdateWindowStatus("Fingerprint enrollment was cancelled.");
                CompleteError("Fingerprint enrollment was cancelled by the reader.");
            }
        }

        private void EnrollmentControl_OnComplete(object Control, string ReaderSerialNumber, int Finger)
        {
            _readerSerialNumber = ReaderSerialNumber ?? _readerSerialNumber;
            RefreshTimeout();
            UpdateWindowStatus("Fingerprint scan complete. Saving template...");
            WriteProgress("Fingerprint scan complete. Saving template...", 1);
        }

        private void EnrollmentControl_OnDelete(object Control, int Finger, ref EventHandlerStatus Status)
        {
            Status = EventHandlerStatus.Success;
        }

        private void EnrollmentControl_OnEnroll(object Control, int Finger, Template Template, ref EventHandlerStatus Status)
        {
            try
            {
                using var stream = new MemoryStream();
                Template.Serialize(stream);
                Status = EventHandlerStatus.Success;
                CompleteSuccess(new[]
                {
                    "status=ok",
                    $"reader={Program.Escape(_readerSerialNumber)}",
                    $"detail={Program.Escape(_readerSerialNumber)}",
                    "quality=100",
                    "format=DPFP.Template",
                    "encoding=Base64",
                    $"template={Convert.ToBase64String(stream.ToArray())}"
                });
            }
            catch (Exception ex)
            {
                Status = EventHandlerStatus.Failure;
                CompleteError($"Unable to save fingerprint template: {ex.Message}");
            }
        }

        private void RefreshTimeout()
        {
            _timeoutTimer.Stop();
            _timeoutTimer.Start();
        }

        private void WriteProgress(string message, int completedCount)
        {
            UpdateWindowStatus(message);
            Program.WriteSharedOutput(_outputPath, new[]
            {
                "status=progress",
                $"reader={Program.Escape(_readerSerialNumber)}",
                $"detail={Program.Escape(_readerSerialNumber)}",
                $"scanCount={completedCount}",
                "scanTarget=1",
                $"message={Program.Escape(message)}"
            });
        }

        private void UpdateWindowStatus(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => UpdateWindowStatus(message)));
                return;
            }

            _statusLabel.Text = message;
        }

        private void CompleteSuccess(IEnumerable<string> lines)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            _timeoutTimer.Stop();
            Program.WriteSharedOutput(_outputPath, lines);
            Close();
        }

        private void CompleteError(string message)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            _timeoutTimer.Stop();
            Program.WriteSharedOutput(_outputPath, new[]
            {
                "status=error",
                $"message={Program.Escape(message)}"
            });
            Close();
        }
    }

    internal abstract class BridgeFormBase : Form, DPFP.Capture.EventHandler
    {
        private readonly string _outputPath;
        private readonly int _timeoutMs;
        private readonly System.Windows.Forms.Timer _timeoutTimer;
        private readonly Label _statusLabel;
        private readonly Label _instructionLabel;
        private readonly Label _statusCaptionLabel;
        private readonly Panel _statusPanel;
        private readonly Button _primaryButton;
        private System.EventHandler? _primaryButtonHandler;
        private Capture? _capturer;
        private bool _completed;
        private string _selectedReaderSerialNumber = string.Empty;

        protected BridgeFormBase(string outputPath, int timeoutMs)
        {
            _outputPath = outputPath;
            _timeoutMs = timeoutMs;
            Text = "HRMS Fingerprint Capture";
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(520, 248);
            MinimumSize = new Size(520, 248);
            MaximizeBox = false;
            MinimizeBox = false;
            TopMost = true;
            BackColor = Color.FromArgb(246, 249, 252);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            WindowState = FormWindowState.Normal;

            var headerPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(520, 62),
                BackColor = Color.FromArgb(31, 73, 125)
            };

            var titleLabel = new Label
            {
                AutoSize = true,
                Location = new Point(18, 14),
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                Text = "Capture Fingerprint"
            };

            var subTitleLabel = new Label
            {
                AutoSize = true,
                Location = new Point(18, 37),
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(220, 232, 245),
                Text = "HRMS biometric enrollment"
            };

            _instructionLabel = new Label
            {
                AutoSize = false,
                Location = new Point(20, 78),
                Size = new Size(480, 34),
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(38, 50, 56),
                Text = "Place one finger flat on the scanner and hold it steady until capture completes."
            };

            _statusPanel = new Panel
            {
                Location = new Point(20, 118),
                Size = new Size(480, 42),
                BackColor = Color.FromArgb(237, 244, 255),
                BorderStyle = BorderStyle.FixedSingle
            };

            _statusCaptionLabel = new Label
            {
                AutoSize = true,
                Location = new Point(12, 12),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(90, 108, 128),
                Text = "STATUS"
            };

            _statusLabel = new Label
            {
                AutoSize = false,
                Location = new Point(80, 10),
                Size = new Size(384, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(24, 119, 242),
                Text = "Initializing fingerprint capture..."
            };

            var footerLabel = new Label
            {
                AutoSize = false,
                Location = new Point(20, 166),
                Size = new Size(300, 16),
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(112, 128, 144),
                Text = "Tip: use one clear finger press and keep it steady for a moment."
            };

            _primaryButton = new Button
            {
                Location = new Point(332, 192),
                Size = new Size(168, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(46, 157, 91),
                ForeColor = Color.White,
                Text = "Complete Enrollment",
                Enabled = false,
                Visible = true
            };
            _primaryButton.FlatAppearance.BorderSize = 0;

            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(subTitleLabel);
            _statusPanel.Controls.Add(_statusCaptionLabel);
            _statusPanel.Controls.Add(_statusLabel);

            Controls.Add(headerPanel);
            Controls.Add(_instructionLabel);
            Controls.Add(_statusPanel);
            Controls.Add(footerLabel);
            Controls.Add(_primaryButton);

            ApplyStatusStyle("Initializing fingerprint capture...");
            _timeoutTimer = new System.Windows.Forms.Timer { Interval = Math.Max(1000, timeoutMs) };
            _timeoutTimer.Tick += (_, _) => CompleteError("Fingerprint capture timed out.");
        }

        protected string OutputPath => _outputPath;
        protected string SelectedReaderSerialNumber => _selectedReaderSerialNumber;

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            BeginInvoke(new Action(StartCaptureFlow));
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_completed)
            {
                _completed = true;
                _timeoutTimer.Stop();
                StopCapture();
                WriteOutput(new[]
                {
                    "status=cancelled",
                    "message=Fingerprint capture cancelled."
                });
            }

            base.OnFormClosing(e);
        }

        private void StartCaptureFlow()
        {
            try
            {
                _selectedReaderSerialNumber = ResolveFirstReaderSerialNumber();
                _capturer = new Capture();
                _capturer.EventHandler = this;
                _capturer.StartCapture();
                _timeoutTimer.Start();
                OnCaptureStarted();
            }
            catch (Exception ex)
            {
                CompleteError($"Unable to start fingerprint capture: {ex.Message}");
            }
        }

        protected void CompleteSuccess(IEnumerable<string> lines)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            _timeoutTimer.Stop();
            StopCapture();
            UpdateWindowStatus("Fingerprint captured successfully.");
            WriteOutput(lines);
            Close();
        }

        protected void CompleteError(string message)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            _timeoutTimer.Stop();
            StopCapture();
            UpdateWindowStatus(message);
            WriteOutput(new[]
            {
                "status=error",
                $"message={Escape(message)}"
            });
            Close();
        }

        protected void StopCapture()
        {
            if (_capturer == null)
            {
                return;
            }

            try
            {
                _capturer.StopCapture();
            }
            catch
            {
            }
        }

        protected static FeatureSet? ExtractFeatures(Sample sample, DataPurpose purpose, out CaptureFeedback feedback)
        {
            feedback = CaptureFeedback.None;
            var extractor = new FeatureExtraction();
            var features = new FeatureSet();
            extractor.CreateFeatureSet(sample, purpose, ref feedback, ref features);
            return feedback == CaptureFeedback.Good ? features : null;
        }

        protected static string Escape(string value)
            => (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();

        protected void WriteOutput(IEnumerable<string> lines)
        {
            Program.WriteSharedOutput(_outputPath, lines);
        }

        protected void UpdateWindowStatus(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => UpdateWindowStatus(message)));
                return;
            }

            _statusLabel.Text = message;
            ApplyStatusStyle(message);
        }

        protected void SetPrimaryAction(string text, bool visible, bool enabled, System.EventHandler? handler)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetPrimaryAction(text, visible, enabled, handler)));
                return;
            }

            if (_primaryButtonHandler != null)
            {
                _primaryButton.Click -= _primaryButtonHandler;
            }

            _primaryButton.Text = text;
            _primaryButton.Visible = visible;
            _primaryButton.Enabled = enabled;
            _primaryButtonHandler = handler ?? PrimaryButtonNoOp;
            _primaryButton.Click += _primaryButtonHandler;
        }

        private void PrimaryButtonNoOp(object? sender, EventArgs e)
        {
        }

        private void ApplyStatusStyle(string message)
        {
            var text = message ?? string.Empty;
            if (text.IndexOf("success", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("good", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("ready", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("captured", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _statusPanel.BackColor = Color.FromArgb(235, 248, 239);
                _statusLabel.ForeColor = Color.FromArgb(34, 120, 72);
                return;
            }

            if (text.IndexOf("poor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("unable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("disconnected", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _statusPanel.BackColor = Color.FromArgb(255, 243, 243);
                _statusLabel.ForeColor = Color.FromArgb(198, 63, 63);
                return;
            }

            _statusPanel.BackColor = Color.FromArgb(237, 244, 255);
            _statusLabel.ForeColor = Color.FromArgb(24, 119, 242);
        }

        private static string ResolveFirstReaderSerialNumber()
        {
            try
            {
                var readers = new ReadersCollection();
                readers.Refresh();
                return readers.Values.Cast<ReaderDescription>()
                    .Select(x => x.SerialNumber)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                    ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        protected void RefreshTimeout()
        {
            _timeoutTimer.Stop();
            _timeoutTimer.Start();
        }

        protected void StopTimeout()
        {
            _timeoutTimer.Stop();
        }

        public void OnComplete(object Capture, string ReaderSerialNumber, Sample Sample)
        {
            RefreshTimeout();
            HandleSample(ReaderSerialNumber, Sample);
        }

        public void OnFingerGone(object Capture, string ReaderSerialNumber)
        {
            RefreshTimeout();
            HandleFingerGone(ReaderSerialNumber);
        }

        public void OnFingerTouch(object Capture, string ReaderSerialNumber)
        {
            RefreshTimeout();
            HandleFingerTouch(ReaderSerialNumber);
        }

        public void OnReaderConnect(object Capture, string ReaderSerialNumber)
        {
            HandleReaderConnect(ReaderSerialNumber);
        }

        public void OnReaderDisconnect(object Capture, string ReaderSerialNumber)
        {
            HandleReaderDisconnect(ReaderSerialNumber);
        }

        public void OnSampleQuality(object Capture, string ReaderSerialNumber, CaptureFeedback CaptureFeedback)
        {
            RefreshTimeout();
            HandleSampleQuality(ReaderSerialNumber, CaptureFeedback);
        }

        protected virtual void OnCaptureStarted()
        {
        }

        protected virtual void HandleFingerGone(string readerSerialNumber)
        {
        }

        protected virtual void HandleFingerTouch(string readerSerialNumber)
        {
        }

        protected virtual void HandleReaderConnect(string readerSerialNumber)
        {
        }

        protected virtual void HandleReaderDisconnect(string readerSerialNumber)
        {
        }

        protected virtual void HandleSampleQuality(string readerSerialNumber, CaptureFeedback captureFeedback)
        {
        }

        protected abstract void HandleSample(string readerSerialNumber, Sample sample);
    }

    internal sealed class CaptureTemplateForm : BridgeFormBase
    {
        private readonly Enrollment _enrollment = new Enrollment();
        private string _readerSerialNumber = string.Empty;
        private int? _qualityScore;
        private int _successfulCaptureCount;
        private string[]? _pendingSuccessLines;
        private const int TargetCaptureCount = 1;

        public CaptureTemplateForm(string outputPath, int timeoutMs)
            : base(outputPath, timeoutMs)
        {
        }

        protected override void OnCaptureStarted()
        {
            SetPrimaryAction("Complete Enrollment", visible: true, enabled: false, handler: null);
            UpdateWindowStatus("Waiting for one clear fingerprint scan...");
            WriteProgress("Waiting for one clear fingerprint scan...", _successfulCaptureCount);
        }

        protected override void HandleSample(string readerSerialNumber, Sample sample)
        {
            _readerSerialNumber = readerSerialNumber ?? string.Empty;
            var features = ExtractFeatures(sample, DataPurpose.Enrollment, out var feedback);
            _qualityScore = feedback == CaptureFeedback.Good ? 100 : 0;
            if (features == null)
            {
                UpdateWindowStatus("Poor fingerprint quality. Use the same finger and press more firmly.");
                WriteProgress("Poor fingerprint quality. Use the same finger and press more firmly.", _successfulCaptureCount);
                return;
            }

            try
            {
                _enrollment.AddFeatures(features);
                _successfulCaptureCount++;
                WriteProgress(
                    "Fingerprint captured successfully.",
                    Math.Min(_successfulCaptureCount, TargetCaptureCount));
            }
            catch (Exception ex)
            {
                CompleteError($"Unable to add fingerprint features: {ex.Message}");
                return;
            }

            switch (_enrollment.TemplateStatus)
            {
                case Enrollment.Status.Ready:
                    using (var stream = new MemoryStream())
                    {
                        StopCaptureForReview();
                        _enrollment.Template.Serialize(stream);
                        _pendingSuccessLines =
                        [
                            "status=ok",
                            $"reader={Escape(_readerSerialNumber)}",
                            $"detail={Escape(_readerSerialNumber)}",
                            $"quality={_qualityScore.GetValueOrDefault()}",
                            "format=DPFP.Template",
                            "encoding=Base64",
                            $"template={Convert.ToBase64String(stream.ToArray())}"
                        ];

                        UpdateWindowStatus("Fingerprint captured successfully. Click Complete Enrollment.");
                        WriteProgress("Fingerprint captured successfully. Click Complete Enrollment.", TargetCaptureCount);
                        SetPrimaryAction("Complete Enrollment", visible: true, enabled: true, handler: ConfirmEnrollment);
                    }
                    break;

                case Enrollment.Status.Failed:
                    _enrollment.Clear();
                    CompleteError("Fingerprint enrollment failed. Try scanning the same finger again.");
                    break;
            }
        }

        private void WriteProgress(string message, int completedCount)
        {
            WriteOutput(new[]
            {
                "status=progress",
                $"reader={Escape(string.IsNullOrWhiteSpace(_readerSerialNumber) ? SelectedReaderSerialNumber : _readerSerialNumber)}",
                $"detail={Escape(string.IsNullOrWhiteSpace(_readerSerialNumber) ? SelectedReaderSerialNumber : _readerSerialNumber)}",
                $"scanCount={completedCount}",
                $"scanTarget={TargetCaptureCount}",
                $"message={Escape(message)}"
            });
        }

        private void StopCaptureForReview()
        {
            StopTimeout();
            StopCapture();
            SetPrimaryAction("Complete Enrollment", visible: true, enabled: true, handler: ConfirmEnrollment);
        }

        private void ConfirmEnrollment(object? sender, EventArgs e)
        {
            if (_pendingSuccessLines == null)
            {
                CompleteError("No captured fingerprint is ready to save.");
                return;
            }

            CompleteSuccess(_pendingSuccessLines);
        }

        protected override void HandleFingerTouch(string readerSerialNumber)
        {
            _readerSerialNumber = readerSerialNumber ?? _readerSerialNumber;
            UpdateWindowStatus("Finger detected. Hold steady for capture.");
                WriteProgress(
                    "Finger detected. Hold steady for capture.",
                    _successfulCaptureCount);
        }

        protected override void HandleFingerGone(string readerSerialNumber)
        {
            _readerSerialNumber = readerSerialNumber ?? _readerSerialNumber;
            if (_successfulCaptureCount < TargetCaptureCount)
            {
                UpdateWindowStatus("Finger removed before capture completed. Place the finger again.");
                WriteProgress(
                    "Finger removed before capture completed. Place the finger again.",
                    _successfulCaptureCount);
            }
        }

        protected override void HandleReaderConnect(string readerSerialNumber)
        {
            _readerSerialNumber = readerSerialNumber ?? _readerSerialNumber;
            UpdateWindowStatus("Fingerprint reader connected and ready.");
            WriteProgress("Fingerprint reader connected and ready.", _successfulCaptureCount);
        }

        protected override void HandleReaderDisconnect(string readerSerialNumber)
        {
            _readerSerialNumber = readerSerialNumber ?? _readerSerialNumber;
            UpdateWindowStatus("Fingerprint reader disconnected.");
            WriteProgress("Fingerprint reader disconnected.", _successfulCaptureCount);
        }

        protected override void HandleSampleQuality(string readerSerialNumber, CaptureFeedback captureFeedback)
        {
            _readerSerialNumber = readerSerialNumber ?? _readerSerialNumber;
            if (captureFeedback == CaptureFeedback.Good)
            {
                UpdateWindowStatus("Good quality fingerprint detected.");
                WriteProgress(
                    "Good quality fingerprint detected.",
                    _successfulCaptureCount);
            }
            else
            {
                UpdateWindowStatus("Poor sample quality. Reposition the finger and try again.");
                WriteProgress(
                    "Poor sample quality. Reposition the finger and try again.",
                    _successfulCaptureCount);
            }
        }
    }

    internal sealed class IdentifyTemplateForm : BridgeFormBase
    {
        private readonly List<ManifestTemplate> _templates;
        private readonly Verification _verification = new Verification();

        public IdentifyTemplateForm(string outputPath, string manifestPath, int timeoutMs)
            : base(outputPath, timeoutMs)
        {
            _templates = LoadManifest(manifestPath);
            if (_templates.Count == 0)
            {
                throw new InvalidOperationException("The fingerprint template manifest is empty.");
            }
        }

        protected override void HandleSample(string readerSerialNumber, Sample sample)
        {
            var features = ExtractFeatures(sample, DataPurpose.Verification, out _);
            if (features == null)
            {
                return;
            }

            ManifestTemplate? bestTemplate = null;
            var bestFar = int.MaxValue;

            foreach (var candidate in _templates)
            {
                try
                {
                    using var stream = new MemoryStream(candidate.TemplateData);
                    var template = new Template(stream);
                    var result = new Verification.Result();
                    _verification.Verify(features, template, ref result);
                    if (result.Verified && result.FARAchieved < bestFar)
                    {
                        bestFar = result.FARAchieved;
                        bestTemplate = candidate;
                    }
                }
                catch
                {
                }
            }

            if (bestTemplate == null)
            {
                CompleteSuccess(new[]
                {
                    "status=no_match",
                    $"reader={Escape(readerSerialNumber)}",
                    $"detail={Escape(readerSerialNumber)}"
                });
                return;
            }

            CompleteSuccess(new[]
            {
                "status=matched",
                $"reader={Escape(readerSerialNumber)}",
                $"detail={Escape(readerSerialNumber)}",
                $"enrollmentId={bestTemplate.EnrollmentId}",
                $"matchScore={bestFar}"
            });
        }

        private static List<ManifestTemplate> LoadManifest(string manifestPath)
        {
            var result = new List<ManifestTemplate>();
            foreach (var rawLine in File.ReadAllLines(manifestPath))
            {
                var line = (rawLine ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var parts = line.Split('|');
                if (parts.Length < 2 || !int.TryParse(parts[0], out var enrollmentId))
                {
                    continue;
                }

                try
                {
                    result.Add(new ManifestTemplate(enrollmentId, Convert.FromBase64String(parts[1])));
                }
                catch
                {
                }
            }

            return result;
        }
    }

    internal sealed class ManifestTemplate
    {
        public ManifestTemplate(int enrollmentId, byte[] templateData)
        {
            EnrollmentId = enrollmentId;
            TemplateData = templateData;
        }

        public int EnrollmentId { get; }
        public byte[] TemplateData { get; }
    }
}
