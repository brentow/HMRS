using System;
using System.IO;

namespace HRMS.Model
{
    public static class StartMenuShortcutService
    {
        public static void EnsureInstalledShortcutUsesHrmsIcon()
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath) || !IsInstalledPath(exePath))
            {
                return;
            }

            var workingDirectory = Path.GetDirectoryName(exePath);
            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                return;
            }

            var shortcutPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft",
                "Windows",
                "Start Menu",
                "Programs",
                "HRMS.lnk");

            var preferredIconPath = Path.Combine(workingDirectory, "ePRIME_logo.ico");
            var iconLocation = File.Exists(preferredIconPath)
                ? preferredIconPath
                : $"{exePath},0";

            try
            {
                if (File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
                }

                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType is null)
                {
                    return;
                }

                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = exePath;
                shortcut.WorkingDirectory = workingDirectory;
                shortcut.IconLocation = iconLocation;
                shortcut.Description = "Human Resources Management System";
                shortcut.Save();
            }
            catch
            {
                // Shortcut repair should never block app startup.
            }
        }

        private static bool IsInstalledPath(string exePath)
        {
            return exePath.Contains(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), string.Empty), StringComparison.OrdinalIgnoreCase)
                || exePath.Contains(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), string.Empty), StringComparison.OrdinalIgnoreCase)
                || exePath.Contains(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"), StringComparison.OrdinalIgnoreCase);
        }
    }
}
