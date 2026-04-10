using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public sealed record BiometricCapturedTemplate(
        byte[] TemplateData,
        string TemplateFormat,
        string TemplateEncoding,
        int? QualityScore,
        string ReaderName,
        string ReaderDetail);

    public sealed record BiometricStoredTemplate(
        int EnrollmentId,
        int EmployeeId,
        string EmployeeNo,
        string EmployeeName,
        string BiometricUserId,
        int? DeviceId,
        string DeviceName,
        byte[] TemplateData,
        string TemplateFormat,
        string TemplateEncoding);

    public sealed record BiometricMatchedEnrollment(
        BiometricStoredTemplate Enrollment,
        int MatchScore,
        string ReaderName,
        string ReaderDetail);

    public sealed class DigitalPersonaRuntimeService
    {
        private const int DefaultCaptureTimeoutMs = 15000;
        private const int DefaultResolution = 500;
        private const int DefaultMatchThreshold = 21474;

        private static readonly object LoadGate = new();
        private static readonly string[] KnownSdkAssemblies =
        [
            "DPUruNet.dll",
            "DPFPDevNET.dll",
            "DPFPVerNet.dll",
            "DPFPShrNET.dll",
            "DPFPGuiNET.dll",
            "DigitalPersona.dll"
        ];

        private static bool _resolveHookRegistered;
        private static string? _sdkDirectory;
        private static SdkContext? _cachedContext;

        public Task<BiometricCapturedTemplate> CaptureTemplateAsync(int timeoutMs = DefaultCaptureTimeoutMs)
            => Task.Run(() => CaptureTemplate(timeoutMs));

        public Task<BiometricMatchedEnrollment?> IdentifyAsync(
            IReadOnlyList<BiometricStoredTemplate> gallery,
            int timeoutMs = DefaultCaptureTimeoutMs)
            => Task.Run(() => Identify(gallery, timeoutMs));

        private static BiometricCapturedTemplate CaptureTemplate(int timeoutMs)
        {
            var context = EnsureSdkContext();
            using var session = OpenFirstReader(context);
            var probe = CaptureProbeTemplate(context, session, timeoutMs);

            return new BiometricCapturedTemplate(
                probe.TemplateData,
                probe.TemplateFormat,
                probe.TemplateEncoding,
                probe.QualityScore,
                probe.ReaderName,
                probe.ReaderDetail);
        }

        private static BiometricMatchedEnrollment? Identify(
            IReadOnlyList<BiometricStoredTemplate> gallery,
            int timeoutMs)
        {
            if (gallery == null || gallery.Count == 0)
            {
                throw new InvalidOperationException("No enrolled fingerprint templates are available for matching.");
            }

            var context = EnsureSdkContext();
            using var session = OpenFirstReader(context);
            var probe = CaptureProbeTemplate(context, session, timeoutMs);

            BiometricStoredTemplate? bestEnrollment = null;
            var bestScore = int.MaxValue;

            foreach (var candidate in gallery)
            {
                if (candidate.TemplateData == null || candidate.TemplateData.Length == 0)
                {
                    continue;
                }

                var candidateFmd = DeserializeTemplate(context, candidate.TemplateData, candidate.TemplateFormat, candidate.TemplateEncoding);
                var score = CompareTemplates(context, probe.TemplateObject, candidateFmd);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestEnrollment = candidate;
                }
            }

            if (bestEnrollment == null || bestScore > DefaultMatchThreshold)
            {
                return null;
            }

            return new BiometricMatchedEnrollment(bestEnrollment, bestScore, probe.ReaderName, probe.ReaderDetail);
        }

        private static SdkContext EnsureSdkContext()
        {
            lock (LoadGate)
            {
                if (_cachedContext != null)
                {
                    return _cachedContext;
                }

                var sdkDllPath = ResolvePrimarySdkAssemblyPath();
                if (sdkDllPath == null)
                {
                    throw new InvalidOperationException(
                        "HID DigitalPersona SDK/runtime was not found. Copy the SDK DLLs into the HRMS folder or install them into Program Files.");
                }

                _sdkDirectory = Path.GetDirectoryName(sdkDllPath);
                if (!_resolveHookRegistered)
                {
                    AppDomain.CurrentDomain.AssemblyResolve += ResolveSdkDependency;
                    _resolveHookRegistered = true;
                }

                PreloadKnownAssemblies(_sdkDirectory);
                var dpuAssembly = Assembly.LoadFrom(sdkDllPath);

                _cachedContext = new SdkContext(
                    DpuAssembly: dpuAssembly,
                    ReaderCollectionType: RequireType(dpuAssembly, "DPUruNet.ReaderCollection"),
                    FeatureExtractionType: RequireType(dpuAssembly, "DPUruNet.FeatureExtraction"),
                    ComparisonType: RequireType(dpuAssembly, "DPUruNet.Comparison"),
                    FidType: RequireType(dpuAssembly, "DPUruNet.Fid"),
                    FmdType: RequireType(dpuAssembly, "DPUruNet.Fmd"),
                    CapturePriorityType: RequireType(dpuAssembly, "DPUruNet.Constants+CapturePriority"),
                    CaptureProcessingType: RequireType(dpuAssembly, "DPUruNet.Constants+CaptureProcessing"));

                return _cachedContext;
            }
        }

        private static string? ResolvePrimarySdkAssemblyPath()
        {
            foreach (var root in GetSdkSearchRoots())
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                {
                    continue;
                }

                var candidate = Path.Combine(root, "DPUruNet.dll");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
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

        private static void PreloadKnownAssemblies(string? sdkDirectory)
        {
            if (string.IsNullOrWhiteSpace(sdkDirectory) || !Directory.Exists(sdkDirectory))
            {
                return;
            }

            foreach (var assemblyName in KnownSdkAssemblies)
            {
                try
                {
                    var path = Path.Combine(sdkDirectory, assemblyName);
                    if (File.Exists(path))
                    {
                        Assembly.LoadFrom(path);
                    }
                }
                catch
                {
                    // Ignore optional runtime DLL load failures during probing.
                }
            }
        }

        private static Assembly? ResolveSdkDependency(object? sender, ResolveEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(_sdkDirectory))
            {
                return null;
            }

            var simpleName = new AssemblyName(args.Name).Name;
            if (string.IsNullOrWhiteSpace(simpleName))
            {
                return null;
            }

            var dependencyPath = Path.Combine(_sdkDirectory, $"{simpleName}.dll");
            return File.Exists(dependencyPath) ? Assembly.LoadFrom(dependencyPath) : null;
        }

        private static ReaderSession OpenFirstReader(SdkContext context)
        {
            var readers = GetReaders(context).ToList();
            if (readers.Count == 0)
            {
                throw new InvalidOperationException("The DigitalPersona SDK did not expose any connected fingerprint readers.");
            }

            var reader = readers[0];
            var readerName = TryGetNestedString(reader, "Description", "Name")
                ?? TryGetString(reader, "Name")
                ?? "Fingerprint Reader";
            var readerDetail = TryGetNestedString(reader, "Description", "SerialNumber")
                ?? readerName;

            TryInvoke(reader, "Open", BuildOpenArguments(context, reader));
            return new ReaderSession(reader, readerName, readerDetail);
        }

        private static IEnumerable<object> GetReaders(SdkContext context)
        {
            var getReadersMethod = context.ReaderCollectionType.GetMethod("GetReaders", BindingFlags.Public | BindingFlags.Static);
            object? collection = getReadersMethod?.Invoke(null, null);

            if (collection == null)
            {
                var readerCollection = Activator.CreateInstance(context.ReaderCollectionType);
                collection = context.ReaderCollectionType
                    .GetMethod("GetReaders", BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(readerCollection, null);
            }

            if (collection is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item != null)
                    {
                        yield return item;
                    }
                }
            }
        }

        private static object?[] BuildOpenArguments(SdkContext context, object reader)
        {
            var openMethod = reader.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(x => string.Equals(x.Name, "Open", StringComparison.OrdinalIgnoreCase));

            if (openMethod == null)
            {
                return Array.Empty<object?>();
            }

            var args = new object?[openMethod.GetParameters().Length];
            for (var i = 0; i < openMethod.GetParameters().Length; i++)
            {
                var parameter = openMethod.GetParameters()[i];
                if (parameter.ParameterType == context.CapturePriorityType)
                {
                    args[i] = GetPreferredEnumValue(context.CapturePriorityType, "DP_PRIORITY_COOPERATIVE", "COOPERATIVE");
                }
                else if (parameter.HasDefaultValue)
                {
                    args[i] = parameter.DefaultValue;
                }
                else
                {
                    args[i] = GetDefaultValue(parameter.ParameterType);
                }
            }

            return args;
        }

        private static CapturedProbe CaptureProbeTemplate(SdkContext context, ReaderSession session, int timeoutMs)
        {
            var captureMethod = SelectCaptureMethod(session.Reader.GetType());
            if (captureMethod == null)
            {
                throw new InvalidOperationException("The DigitalPersona runtime did not expose a supported capture method.");
            }

            var captureResult = InvokeReaderCapture(captureMethod, session.Reader, context, timeoutMs);
            var capturePayload = ExtractCapturePayload(captureResult);
            var fmdObject = CreateFmdFromFid(context, capturePayload.FidObject);
            var serializedTemplate = SerializeTemplate(fmdObject);

            return new CapturedProbe(
                TemplateObject: fmdObject,
                TemplateData: serializedTemplate.TemplateData,
                TemplateFormat: serializedTemplate.TemplateFormat,
                TemplateEncoding: serializedTemplate.TemplateEncoding,
                QualityScore: capturePayload.QualityScore,
                ReaderName: session.ReaderName,
                ReaderDetail: session.ReaderDetail);
        }

        private static MethodInfo? SelectCaptureMethod(Type readerType)
        {
            return readerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => string.Equals(x.Name, "Capture", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(x.Name, "CaptureAsync", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => string.Equals(x.Name, "Capture", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenByDescending(x => x.GetParameters().Length)
                .FirstOrDefault();
        }

        private static object InvokeReaderCapture(MethodInfo method, object reader, SdkContext context, int timeoutMs)
        {
            var args = BuildCaptureArguments(method, context, reader, timeoutMs);
            var result = method.Invoke(reader, args);

            if (result is Task task)
            {
                task.GetAwaiter().GetResult();
                return task.GetType().GetProperty("Result")?.GetValue(task)
                    ?? throw new InvalidOperationException("Fingerprint capture did not return a capture result.");
            }

            return result ?? throw new InvalidOperationException("Fingerprint capture returned no result.");
        }

        private static object?[] BuildCaptureArguments(MethodInfo method, SdkContext context, object reader, int timeoutMs)
        {
            var resolution = GetFirstResolution(reader) ?? DefaultResolution;
            var args = new object?[method.GetParameters().Length];
            var fidFormatType = GetNestedType(context.FidType, "Format");
            var fidFormat = GetPreferredEnumValue(fidFormatType, "ANSI_381_2004", "ANSI", "ISO_19794_4_2005");
            var captureProcessing = GetPreferredEnumValue(context.CaptureProcessingType, "DP_IMG_PROC_DEFAULT");

            for (var i = 0; i < method.GetParameters().Length; i++)
            {
                var parameter = method.GetParameters()[i];
                var parameterName = parameter.Name ?? string.Empty;

                if (parameter.ParameterType.IsEnum)
                {
                    if (parameter.ParameterType == fidFormatType)
                    {
                        args[i] = fidFormat;
                        continue;
                    }

                    if (parameter.ParameterType == context.CaptureProcessingType)
                    {
                        args[i] = captureProcessing;
                        continue;
                    }
                }

                if (parameter.ParameterType == typeof(int))
                {
                    if (parameterName.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                    {
                        args[i] = timeoutMs;
                    }
                    else if (parameterName.Contains("resolution", StringComparison.OrdinalIgnoreCase))
                    {
                        args[i] = resolution;
                    }
                    else
                    {
                        args[i] = i == method.GetParameters().Length - 1 ? timeoutMs : resolution;
                    }

                    continue;
                }

                if (parameter.ParameterType == typeof(uint))
                {
                    args[i] = unchecked((uint)(parameterName.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                        ? Math.Max(timeoutMs, 0)
                        : Math.Max(resolution, 0)));
                    continue;
                }

                if (parameter.HasDefaultValue)
                {
                    args[i] = parameter.DefaultValue;
                }
                else
                {
                    args[i] = GetDefaultValue(parameter.ParameterType);
                }
            }

            return args;
        }

        private static int? GetFirstResolution(object reader)
        {
            var capabilities = reader.GetType().GetProperty("Capabilities", BindingFlags.Public | BindingFlags.Instance)?.GetValue(reader);
            var resolutions = capabilities?.GetType().GetProperty("Resolutions", BindingFlags.Public | BindingFlags.Instance)?.GetValue(capabilities);
            if (resolutions is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item is int value)
                    {
                        return value;
                    }

                    if (item != null && int.TryParse(item.ToString(), out var parsed))
                    {
                        return parsed;
                    }
                }
            }

            return null;
        }

        private static CapturePayload ExtractCapturePayload(object captureResult)
        {
            var resultCodeValue = TryGetPropertyValue(captureResult, "ResultCode");
            if (resultCodeValue != null)
            {
                var normalized = resultCodeValue.ToString()?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(normalized) &&
                    !string.Equals(normalized, "DP_SUCCESS", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(normalized, "Success", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(normalized, "0", StringComparison.OrdinalIgnoreCase))
                {
                    var message = TryGetPropertyValue(captureResult, "Message")?.ToString()
                        ?? $"Fingerprint capture failed with result code {normalized}.";
                    throw new InvalidOperationException(message);
                }
            }

            var fidObject = TryGetPropertyValue(captureResult, "Data")
                ?? throw new InvalidOperationException("Fingerprint capture did not return image data.");

            return new CapturePayload(
                FidObject: fidObject,
                QualityScore: TryConvertToInt(TryGetPropertyValue(captureResult, "Quality")));
        }

        private static object CreateFmdFromFid(SdkContext context, object fidObject)
        {
            var extractionMethod = context.FeatureExtractionType
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                .FirstOrDefault(x => string.Equals(x.Name, "CreateFmdFromFid", StringComparison.OrdinalIgnoreCase));

            if (extractionMethod == null)
            {
                throw new InvalidOperationException("The DigitalPersona runtime did not expose fingerprint template extraction.");
            }

            var target = extractionMethod.IsStatic ? null : Activator.CreateInstance(context.FeatureExtractionType);
            var args = BuildFmdExtractionArguments(extractionMethod, context, fidObject);
            var result = extractionMethod.Invoke(target, args)
                ?? throw new InvalidOperationException("Fingerprint template extraction returned no result.");

            return TryGetPropertyValue(result, "Data") ?? result;
        }

        private static object?[] BuildFmdExtractionArguments(MethodInfo method, SdkContext context, object fidObject)
        {
            var fmdFormatType = GetNestedType(context.FmdType, "Format");
            var fmdFormat = GetPreferredEnumValue(fmdFormatType, "ANSI_378_2004", "ANSI", "ISO_19794_2_2005", "DP_VER_FEATURES");
            var args = new object?[method.GetParameters().Length];

            for (var i = 0; i < method.GetParameters().Length; i++)
            {
                var parameter = method.GetParameters()[i];
                if (parameter.ParameterType.IsAssignableFrom(fidObject.GetType()))
                {
                    args[i] = fidObject;
                }
                else if (parameter.ParameterType.IsEnum && parameter.ParameterType == fmdFormatType)
                {
                    args[i] = fmdFormat;
                }
                else if (parameter.HasDefaultValue)
                {
                    args[i] = parameter.DefaultValue;
                }
                else
                {
                    args[i] = GetDefaultValue(parameter.ParameterType);
                }
            }

            return args;
        }

        private static SerializedTemplate SerializeTemplate(object fmdObject)
        {
            var templateBytes = TryExtractByteArray(fmdObject);
            if (templateBytes != null && templateBytes.Length > 0)
            {
                return new SerializedTemplate(templateBytes, "ANSI_378_2004", "BINARY");
            }

            var xml = TryExtractXml(fmdObject);
            if (!string.IsNullOrWhiteSpace(xml))
            {
                return new SerializedTemplate(Encoding.UTF8.GetBytes(xml), "ANSI_378_2004", "XML");
            }

            throw new InvalidOperationException("HRMS could not serialize the captured fingerprint template for database storage.");
        }

        private static byte[]? TryExtractByteArray(object source)
        {
            foreach (var propertyName in new[] { "Bytes", "Data", "RawData", "Buffer" })
            {
                if (TryGetPropertyValue(source, propertyName) is byte[] bytes && bytes.Length > 0)
                {
                    return bytes;
                }
            }

            foreach (var methodName in new[] { "Serialize", "ToByteArray", "GetData" })
            {
                var candidate = source.GetType()
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(x => string.Equals(x.Name, methodName, StringComparison.OrdinalIgnoreCase) &&
                                         x.GetParameters().Length == 0 &&
                                         x.ReturnType == typeof(byte[]));

                if (candidate?.Invoke(source, null) is byte[] bytes && bytes.Length > 0)
                {
                    return bytes;
                }
            }

            return null;
        }

        private static string? TryExtractXml(object source)
        {
            foreach (var methodName in new[] { "SerializeXml", "ToXml", "ToXmlString" })
            {
                var method = source.GetType()
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(x => string.Equals(x.Name, methodName, StringComparison.OrdinalIgnoreCase) &&
                                         x.GetParameters().Length == 0 &&
                                         x.ReturnType == typeof(string));

                if (method?.Invoke(source, null) is string xml && !string.IsNullOrWhiteSpace(xml))
                {
                    return xml;
                }
            }

            foreach (var propertyName in new[] { "Xml", "XmlData" })
            {
                if (TryGetPropertyValue(source, propertyName) is string xml && !string.IsNullOrWhiteSpace(xml))
                {
                    return xml;
                }
            }

            return null;
        }

        private static object DeserializeTemplate(
            SdkContext context,
            byte[] templateData,
            string? templateFormat,
            string? templateEncoding)
        {
            if (templateData == null || templateData.Length == 0)
            {
                throw new InvalidOperationException("Stored biometric template is empty.");
            }

            var encoding = string.IsNullOrWhiteSpace(templateEncoding)
                ? "BINARY"
                : templateEncoding.Trim().ToUpperInvariant();

            if (encoding == "XML")
            {
                var xml = Encoding.UTF8.GetString(templateData);
                foreach (var factory in EnumerateFactoryMethods(context.FmdType, typeof(string), xml))
                {
                    var value = TryGetPropertyValue(factory, "Data") ?? factory;
                    if (value != null && context.FmdType.IsInstanceOfType(value))
                    {
                        return value;
                    }
                }
            }
            else
            {
                foreach (var factory in EnumerateFactoryMethods(context.FmdType, typeof(byte[]), templateData))
                {
                    var value = TryGetPropertyValue(factory, "Data") ?? factory;
                    if (value != null && context.FmdType.IsInstanceOfType(value))
                    {
                        return value;
                    }
                }
            }

            throw new InvalidOperationException(
                $"HRMS could not reconstruct a stored fingerprint template ({templateEncoding ?? "BINARY"}).");
        }

        private static IEnumerable<object?> EnumerateFactoryMethods(Type fmdType, Type payloadType, object payload)
        {
            foreach (var constructor in fmdType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                var parameters = constructor.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == payloadType)
                {
                    yield return constructor.Invoke(new[] { payload });
                }
            }

            foreach (var method in fmdType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            {
                if (!(method.Name.Contains("Deserialize", StringComparison.OrdinalIgnoreCase) ||
                      method.Name.Contains("Import", StringComparison.OrdinalIgnoreCase) ||
                      method.Name.Contains("From", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != 1 || parameters[0].ParameterType != payloadType)
                {
                    continue;
                }

                var target = method.IsStatic ? null : Activator.CreateInstance(fmdType);
                yield return method.Invoke(target, new[] { payload });
            }
        }

        private static int CompareTemplates(SdkContext context, object probeTemplate, object candidateTemplate)
        {
            var compareMethod = context.ComparisonType
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                .FirstOrDefault(x => string.Equals(x.Name, "Compare", StringComparison.OrdinalIgnoreCase));

            if (compareMethod == null)
            {
                throw new InvalidOperationException("The DigitalPersona runtime did not expose fingerprint comparison.");
            }

            var target = compareMethod.IsStatic ? null : Activator.CreateInstance(context.ComparisonType);
            var args = BuildCompareArguments(compareMethod, probeTemplate, candidateTemplate);
            var result = compareMethod.Invoke(target, args);

            if (TryConvertToInt(result) is int directScore)
            {
                return directScore;
            }

            if (result != null && TryConvertToInt(TryGetPropertyValue(result, "Score")) is int score)
            {
                return score;
            }

            foreach (var arg in args)
            {
                if (TryConvertToInt(arg) is int argScore)
                {
                    return argScore;
                }
            }

            throw new InvalidOperationException("Fingerprint comparison did not return a usable match score.");
        }

        private static object?[] BuildCompareArguments(MethodInfo method, object probeTemplate, object candidateTemplate)
        {
            var args = new object?[method.GetParameters().Length];
            var templateCount = 0;

            for (var i = 0; i < method.GetParameters().Length; i++)
            {
                var parameter = method.GetParameters()[i];
                if (parameter.ParameterType.IsAssignableFrom(probeTemplate.GetType()) && templateCount == 0)
                {
                    args[i] = probeTemplate;
                    templateCount++;
                }
                else if (parameter.ParameterType.IsAssignableFrom(candidateTemplate.GetType()) && templateCount == 1)
                {
                    args[i] = candidateTemplate;
                    templateCount++;
                }
                else if (parameter.ParameterType == typeof(int))
                {
                    args[i] = 0;
                }
                else if (parameter.HasDefaultValue)
                {
                    args[i] = parameter.DefaultValue;
                }
                else
                {
                    args[i] = GetDefaultValue(parameter.ParameterType);
                }
            }

            return args;
        }

        private static Type RequireType(Assembly assembly, string fullName)
        {
            return assembly.GetType(fullName, throwOnError: true, ignoreCase: false)
                ?? throw new InvalidOperationException($"Required SDK type not found: {fullName}");
        }

        private static Type GetNestedType(Type declaringType, string nestedTypeName)
        {
            return declaringType.GetNestedType(nestedTypeName, BindingFlags.Public)
                ?? throw new InvalidOperationException($"Required SDK nested type not found: {declaringType.FullName}+{nestedTypeName}");
        }

        private static object GetPreferredEnumValue(Type enumType, params string[] preferredNames)
        {
            foreach (var name in preferredNames)
            {
                if (Enum.GetNames(enumType).Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
                {
                    return Enum.Parse(enumType, name, ignoreCase: true);
                }
            }

            var fallbackName = Enum.GetNames(enumType).FirstOrDefault()
                ?? throw new InvalidOperationException($"Enum {enumType.FullName} does not define any values.");
            return Enum.Parse(enumType, fallbackName, ignoreCase: true);
        }

        private static object? TryGetPropertyValue(object? source, string propertyName)
        {
            if (source == null)
            {
                return null;
            }

            return source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                ?.GetValue(source);
        }

        private static string? TryGetString(object? source, string propertyName)
            => TryGetPropertyValue(source, propertyName)?.ToString();

        private static string? TryGetNestedString(object? source, string parentPropertyName, string childPropertyName)
        {
            var parent = TryGetPropertyValue(source, parentPropertyName);
            return parent == null ? null : TryGetPropertyValue(parent, childPropertyName)?.ToString();
        }

        private static int? TryConvertToInt(object? value)
        {
            if (value == null)
            {
                return null;
            }

            return int.TryParse(value.ToString(), out var parsed) ? parsed : null;
        }

        private static void TryInvoke(object target, string methodName, object?[] args)
        {
            var method = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(x => string.Equals(x.Name, methodName, StringComparison.OrdinalIgnoreCase) &&
                                     x.GetParameters().Length == args.Length);

            method?.Invoke(target, args);
        }

        private static object? GetDefaultValue(Type type)
            => !type.IsValueType ? null : Activator.CreateInstance(type);

        private sealed record SdkContext(
            Assembly DpuAssembly,
            Type ReaderCollectionType,
            Type FeatureExtractionType,
            Type ComparisonType,
            Type FidType,
            Type FmdType,
            Type CapturePriorityType,
            Type CaptureProcessingType);

        private sealed record ReaderSession(
            object Reader,
            string ReaderName,
            string ReaderDetail) : IDisposable
        {
            public void Dispose()
            {
                try
                {
                    TryInvoke(Reader, "CancelCapture", Array.Empty<object?>());
                }
                catch
                {
                }

                try
                {
                    TryInvoke(Reader, "Close", Array.Empty<object?>());
                }
                catch
                {
                }

                if (Reader is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        private sealed record CapturePayload(object FidObject, int? QualityScore);
        private sealed record SerializedTemplate(byte[] TemplateData, string TemplateFormat, string TemplateEncoding);
        private sealed record CapturedProbe(
            object TemplateObject,
            byte[] TemplateData,
            string TemplateFormat,
            string TemplateEncoding,
            int? QualityScore,
            string ReaderName,
            string ReaderDetail);
    }
}
