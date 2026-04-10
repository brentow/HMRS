using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public sealed record ConnectedBiometricReaderInfo(
        string DeviceName,
        string Manufacturer,
        string InstanceId,
        string LocationInfo,
        string DriverVersion,
        string DriverProvider,
        string DriverService)
    {
        public string SummaryText => $"{DeviceName} ({Manufacturer})";
        public string DetailText => $"Driver {DriverVersion} | Provider {DriverProvider} | Service {DriverService} | {LocationInfo}";
        public string SuggestedSerialNo => TryExtractSerialNo(InstanceId);
        public string SuggestedLocation => string.IsNullOrWhiteSpace(LocationInfo) ? "USB fingerprint reader" : LocationInfo.Trim();

        private static string TryExtractSerialNo(string? instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                return string.Empty;
            }

            var tail = instanceId
                .Split('\\', StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault()
                ?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(tail) || (tail.StartsWith("{", StringComparison.Ordinal) && tail.EndsWith("}", StringComparison.Ordinal)))
            {
                return string.Empty;
            }

            return tail;
        }
    }

    public sealed record BiometricHardwareProbeResult(
        IReadOnlyList<ConnectedBiometricReaderInfo> Readers,
        int WindowsBiometricUnitCount,
        bool HasDigitalPersonaSdkRuntime)
    {
        public bool HasConnectedReaders => Readers.Count > 0;
        public bool IsWbfReady => WindowsBiometricUnitCount > 0;
        public bool SupportsLiveCaptureInHrms => HasDigitalPersonaSdkRuntime;

        public string StatusTitle
        {
            get
            {
                if (!HasConnectedReaders)
                {
                    return "No connected fingerprint reader";
                }

                if (HasDigitalPersonaSdkRuntime)
                {
                    return "Reader detected, SDK runtime found";
                }

                if (IsWbfReady)
                {
                    return "Reader detected through Windows Biometric Framework";
                }

                return "Reader detected, driver-only mode";
            }
        }

        public string StatusText
        {
            get
            {
                if (!HasConnectedReaders)
                {
                    return "HRMS did not find a supported HID / DigitalPersona fingerprint reader on this PC.";
                }

                var primaryReader = Readers[0];
                if (HasDigitalPersonaSdkRuntime)
                {
                    return $"{primaryReader.DeviceName} is connected. The DigitalPersona runtime files were found on this PC.";
                }

                if (IsWbfReady)
                {
                    return $"{primaryReader.DeviceName} is connected and Windows Biometric Framework can see fingerprint units.";
                }

                return $"{primaryReader.DeviceName} is connected on the HID Global driver, but capture SDK/runtime files were not found for HRMS.";
            }
        }

        public string GuidanceText
        {
            get
            {
                if (!HasConnectedReaders)
                {
                    return "Plug in the U.are.U 4500 reader, then click Detect Reader. After the reader appears here, register it into the Biometric Devices table.";
                }

                if (HasDigitalPersonaSdkRuntime)
                {
                    return "The reader can now be used for employee enrollment and live attendance scanning inside HRMS.";
                }

                if (IsWbfReady)
                {
                    return "Windows can see the biometric hardware, but HRMS still needs the HID DigitalPersona SDK/runtime layer for custom employee enrollment and matching.";
                }

                return "The PC has the HID Global device driver only. Register the reader now for attendance device tracking, then add the HID DigitalPersona SDK/runtime to enable live fingerprint capture in HRMS.";
            }
        }
    }

    public sealed class BiometricHardwareService
    {
        private static readonly string[] KnownSdkAssemblies =
        [
            "DPUruNet.dll",
            "DPFPDevNET.dll",
            "DPFPVerNet.dll",
            "DPFPShrNET.dll",
            "DPFPGuiNET.dll",
            "DigitalPersona.dll"
        ];

        public Task<BiometricHardwareProbeResult> ProbeAsync()
        {
            return Task.Run(Probe);
        }

        public BiometricHardwareProbeResult Probe()
        {
            try
            {
                var readers = GetConnectedReaders();
                var wbfUnits = TryCountWindowsBiometricUnits();
                var hasSdkRuntime = FindSdkRuntimeFiles().Count > 0;
                return new BiometricHardwareProbeResult(readers, wbfUnits, hasSdkRuntime);
            }
            catch
            {
                return new BiometricHardwareProbeResult(Array.Empty<ConnectedBiometricReaderInfo>(), 0, false);
            }
        }

        private static IReadOnlyList<ConnectedBiometricReaderInfo> GetConnectedReaders()
        {
            var readers = new List<ConnectedBiometricReaderInfo>();
            var driversByDeviceId = GetSignedDriverMap();

            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, DeviceID, PNPDeviceID, Manufacturer, Service, LocationInformation FROM Win32_PnPEntity");

            foreach (ManagementObject device in searcher.Get())
            {
                var name = ReadString(device, "Name");
                var instanceId = ReadString(device, "PNPDeviceID");
                var manufacturer = ReadString(device, "Manufacturer");
                var service = ReadString(device, "Service");
                var locationInfo = ReadString(device, "LocationInformation");

                if (!IsSupportedReader(name, instanceId, manufacturer, service))
                {
                    continue;
                }

                var driver = driversByDeviceId.TryGetValue(instanceId, out var matchedDriver)
                    ? matchedDriver
                    : SignedDriverInfo.Empty;

                readers.Add(new ConnectedBiometricReaderInfo(
                    DeviceName: string.IsNullOrWhiteSpace(name) ? "Fingerprint Reader" : name.Trim(),
                    Manufacturer: string.IsNullOrWhiteSpace(manufacturer) ? "Unknown" : manufacturer.Trim(),
                    InstanceId: string.IsNullOrWhiteSpace(instanceId) ? ReadString(device, "DeviceID") : instanceId.Trim(),
                    LocationInfo: string.IsNullOrWhiteSpace(locationInfo) ? "USB connection" : locationInfo.Trim(),
                    DriverVersion: driver.DriverVersion,
                    DriverProvider: driver.DriverProvider,
                    DriverService: string.IsNullOrWhiteSpace(service) ? "Unknown" : service.Trim()));
            }

            return readers
                .GroupBy(x => x.InstanceId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(x => x.DeviceName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static Dictionary<string, SignedDriverInfo> GetSignedDriverMap()
        {
            var map = new Dictionary<string, SignedDriverInfo>(StringComparer.OrdinalIgnoreCase);
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID, DriverVersion, DriverProviderName FROM Win32_PnPSignedDriver");

            foreach (ManagementObject driver in searcher.Get())
            {
                var deviceId = ReadString(driver, "DeviceID");
                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    continue;
                }

                map[deviceId.Trim()] = new SignedDriverInfo(
                    DriverVersion: NormalizeText(ReadString(driver, "DriverVersion"), "-"),
                    DriverProvider: NormalizeText(ReadString(driver, "DriverProviderName"), "Unknown"));
            }

            return map;
        }

        private static bool IsSupportedReader(string? name, string? instanceId, string? manufacturer, string? service)
        {
            var normalizedName = name?.Trim() ?? string.Empty;
            var normalizedInstanceId = instanceId?.Trim() ?? string.Empty;
            var normalizedManufacturer = manufacturer?.Trim() ?? string.Empty;
            var normalizedService = service?.Trim() ?? string.Empty;

            return normalizedInstanceId.Contains("VID_05BA&PID_000A", StringComparison.OrdinalIgnoreCase) ||
                   normalizedName.Contains("U.are.U", StringComparison.OrdinalIgnoreCase) ||
                   normalizedName.Contains("DigitalPersona", StringComparison.OrdinalIgnoreCase) ||
                   (normalizedManufacturer.Contains("HID Global", StringComparison.OrdinalIgnoreCase) &&
                    normalizedService.Contains("usbdpfp", StringComparison.OrdinalIgnoreCase));
        }

        private static IReadOnlyList<string> FindSdkRuntimeFiles()
        {
            var candidates = new List<string>();
            foreach (var root in GetSdkSearchRoots())
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                {
                    continue;
                }

                foreach (var assemblyName in KnownSdkAssemblies)
                {
                    try
                    {
                        var exactPath = Path.Combine(root, assemblyName);
                        if (File.Exists(exactPath))
                        {
                            candidates.Add(exactPath);
                            continue;
                        }

                        foreach (var nestedPath in Directory.EnumerateFiles(root, assemblyName, SearchOption.AllDirectories).Take(2))
                        {
                            candidates.Add(nestedPath);
                        }
                    }
                    catch
                    {
                        // Ignore inaccessible folders while probing optional SDK roots.
                    }
                }
            }

            return candidates
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IEnumerable<string> GetSdkSearchRoots()
        {
            yield return AppContext.BaseDirectory;

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                yield return Path.Combine(programFiles, "DigitalPersona");
                yield return Path.Combine(programFiles, "HID Global");
            }

            if (!string.IsNullOrWhiteSpace(programFilesX86))
            {
                yield return Path.Combine(programFilesX86, "DigitalPersona");
                yield return Path.Combine(programFilesX86, "HID Global");
            }
        }

        private static int TryCountWindowsBiometricUnits()
        {
            const uint winbioTypeFingerprint = 0x00000008;
            var hr = WinBioEnumBiometricUnits(winbioTypeFingerprint, out var schemaArray, out var unitCount);
            try
            {
                return hr == 0 ? checked((int)(ulong)unitCount) : 0;
            }
            finally
            {
                if (schemaArray != IntPtr.Zero)
                {
                    WinBioFree(schemaArray);
                }
            }
        }

        private static string ReadString(ManagementBaseObject source, string propertyName)
        {
            try
            {
                return source[propertyName]?.ToString()?.Trim() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string NormalizeText(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        [DllImport("winbio.dll")]
        private static extern int WinBioEnumBiometricUnits(
            uint factor,
            out IntPtr unitSchemaArray,
            out UIntPtr unitCount);

        [DllImport("winbio.dll")]
        private static extern void WinBioFree(IntPtr address);

        private sealed record SignedDriverInfo(string DriverVersion, string DriverProvider)
        {
            public static SignedDriverInfo Empty { get; } = new("-", "Unknown");
        }
    }
}
