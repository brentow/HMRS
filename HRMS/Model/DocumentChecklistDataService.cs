using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record DocumentChecklistItemDto(
        int ChecklistId,
        int EmployeeId,
        string PositionName,
        string EmploymentType,
        string DocumentCode,
        string DocumentName,
        int DocumentTier,
        bool IsRequired,
        string Status,
        DateTime? SubmittedDate,
        DateTime? ExpiryDate,
        DateTime? VerifiedDate,
        string? VerifiedBy,
        string? WaivedReason,
        string? Remarks,
        string? FileName,
        string? FilePath,
        long FileSize,
        DateTime? UploadedAt)
    {
        public string TierLabel => $"Tier {DocumentTier}";
    }

    public record ChecklistAttachmentContentDto(
        string FileName,
        string? FilePath,
        byte[]? FileBlob,
        long FileSize,
        DateTime? UploadedAt);

    public record DocumentChecklistEmployeeSummaryDto(
        int EmployeeId,
        string EmployeeNo,
        string EmployeeName,
        string PositionName,
        string EmploymentType,
        int TotalDocumentCount,
        int VerifiedDocumentCount,
        int SubmittedDocumentCount,
        int WaivedDocumentCount,
        int ExpiredDocumentCount,
        int ExpiringSoonCount,
        string OverallStatus)
    {
        public int SatisfiedDocumentCount => VerifiedDocumentCount + WaivedDocumentCount;
        public string ProgressText => $"{SatisfiedDocumentCount}/{TotalDocumentCount}";
    }

    public record DocumentChecklistStatsDto(
        int TotalEmployees,
        int CompleteEmployees,
        int PartialEmployees,
        int IncompleteEmployees,
        int ExpiringSoonDocuments);

    public record ExpiringDocumentDto(
        int ChecklistId,
        int EmployeeId,
        string EmployeeNo,
        string EmployeeName,
        string DocumentName,
        DateTime ExpiryDate,
        string Status)
    {
        public int DaysRemaining => Math.Max(0, (ExpiryDate.Date - DateTime.Today).Days);
    }

    internal record EmployeeChecklistProfile(
        int EmployeeId,
        string EmployeeNo,
        string EmployeeName,
        string PositionName,
        string EmploymentType);

    public class DocumentChecklistDataService
    {
        private readonly string _connectionString;

        public DocumentChecklistDataService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task EnsureChecklistsForAllEmployeesAsync()
        {
            const string sql = @"
SELECT
    e.employee_id,
    COALESCE(e.employee_no, '-') AS employee_no,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS employee_name,
    COALESCE(p.position_name, '') AS position_name,
    COALESCE(at.type_name, 'Permanent') AS employment_type
FROM employees e
LEFT JOIN positions p ON p.position_id = e.position_id
LEFT JOIN appointment_types at ON at.appointment_type_id = e.appointment_type_id
WHERE NOT EXISTS (
    SELECT 1
    FROM employee_document_checklist c
    WHERE c.employee_id = e.employee_id
)
ORDER BY e.employee_id;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var profiles = new List<EmployeeChecklistProfile>();
            await using (var command = new MySqlCommand(sql, connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    profiles.Add(new EmployeeChecklistProfile(
                        EmployeeId: ToInt(reader["employee_id"]),
                        EmployeeNo: reader["employee_no"]?.ToString() ?? "-",
                        EmployeeName: reader["employee_name"]?.ToString() ?? "-",
                        PositionName: reader["position_name"]?.ToString() ?? string.Empty,
                        EmploymentType: reader["employment_type"]?.ToString() ?? "Permanent"));
                }
            }

            if (profiles.Count == 0)
            {
                return;
            }

            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                foreach (var profile in profiles)
                {
                    await InsertChecklistRowsAsync(connection, transaction, profile.EmployeeId, profile.PositionName, profile.EmploymentType);
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task GenerateChecklistForEmployeeAsync(int employeeId)
        {
            if (employeeId <= 0)
            {
                throw new InvalidOperationException("Invalid employee id.");
            }

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                await GenerateChecklistForEmployeeAsync(connection, transaction, employeeId);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        internal async Task GenerateChecklistForEmployeeAsync(MySqlConnection connection, MySqlTransaction transaction, int employeeId)
        {
            if (employeeId <= 0)
            {
                throw new InvalidOperationException("Invalid employee id.");
            }

            var profile = await GetEmployeeProfileAsync(connection, transaction, employeeId);
            await InsertChecklistRowsAsync(connection, transaction, profile.EmployeeId, profile.PositionName, profile.EmploymentType);
        }

        public async Task GenerateChecklistForEmployeeAsync(int employeeId, string positionName, string employmentType)
        {
            if (employeeId <= 0)
            {
                throw new InvalidOperationException("Invalid employee id.");
            }

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                await InsertChecklistRowsAsync(connection, transaction, employeeId, positionName, employmentType);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<IReadOnlyList<DocumentChecklistEmployeeSummaryDto>> GetEmployeeChecklistSummariesAsync(string? search = null, int limit = 500)
        {
            const string sql = @"
SELECT
    e.employee_id,
    COALESCE(e.employee_no, '-') AS employee_no,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS employee_name,
    COALESCE(p.position_name, '-') AS position_name,
    COALESCE(at.type_name, '-') AS employment_type,
    COUNT(c.checklist_id) AS total_docs,
    SUM(CASE WHEN c.status = 'verified' THEN 1 ELSE 0 END) AS verified_docs,
    SUM(CASE WHEN c.status = 'submitted' THEN 1 ELSE 0 END) AS submitted_docs,
    SUM(CASE WHEN c.status = 'waived' THEN 1 ELSE 0 END) AS waived_docs,
    SUM(CASE WHEN c.status = 'expired' THEN 1 ELSE 0 END) AS expired_docs,
    SUM(CASE
        WHEN c.expiry_date IS NOT NULL
         AND c.expiry_date BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL 30 DAY)
         AND c.status <> 'waived'
        THEN 1 ELSE 0 END) AS expiring_soon_docs
FROM employees e
LEFT JOIN positions p ON p.position_id = e.position_id
LEFT JOIN appointment_types at ON at.appointment_type_id = e.appointment_type_id
LEFT JOIN employee_document_checklist c ON c.employee_id = e.employee_id
WHERE (
    @search = ''
    OR e.employee_no LIKE CONCAT('%', @search, '%')
    OR CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) LIKE CONCAT('%', @search, '%')
    OR COALESCE(p.position_name, '') LIKE CONCAT('%', @search, '%')
    OR COALESCE(at.type_name, '') LIKE CONCAT('%', @search, '%')
)
GROUP BY e.employee_id, e.employee_no, e.last_name, e.first_name, e.middle_name, p.position_name, at.type_name
ORDER BY e.last_name, e.first_name
LIMIT @limit;";

            var rows = new List<DocumentChecklistEmployeeSummaryDto>();
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@search", string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim());
            command.Parameters.AddWithValue("@limit", Math.Max(1, limit));

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var totalDocs = ToInt(reader["total_docs"]);
                var verifiedDocs = ToInt(reader["verified_docs"]);
                var submittedDocs = ToInt(reader["submitted_docs"]);
                var waivedDocs = ToInt(reader["waived_docs"]);
                var expiredDocs = ToInt(reader["expired_docs"]);

                rows.Add(new DocumentChecklistEmployeeSummaryDto(
                    EmployeeId: ToInt(reader["employee_id"]),
                    EmployeeNo: reader["employee_no"]?.ToString() ?? "-",
                    EmployeeName: reader["employee_name"]?.ToString() ?? "-",
                    PositionName: reader["position_name"]?.ToString() ?? "-",
                    EmploymentType: reader["employment_type"]?.ToString() ?? "-",
                    TotalDocumentCount: totalDocs,
                    VerifiedDocumentCount: verifiedDocs,
                    SubmittedDocumentCount: submittedDocs,
                    WaivedDocumentCount: waivedDocs,
                    ExpiredDocumentCount: expiredDocs,
                    ExpiringSoonCount: ToInt(reader["expiring_soon_docs"]),
                    OverallStatus: ResolveOverallStatus(totalDocs, verifiedDocs, submittedDocs, waivedDocs, expiredDocs)));
            }

            return rows
                .OrderBy(x => StatusRank(x.OverallStatus))
                .ThenBy(x => x.EmployeeName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<DocumentChecklistStatsDto> GetChecklistSummaryStatsAsync()
        {
            var rows = await GetEmployeeChecklistSummariesAsync(limit: 5000);
            return new DocumentChecklistStatsDto(
                TotalEmployees: rows.Count,
                CompleteEmployees: rows.Count(x => string.Equals(x.OverallStatus, "Complete", StringComparison.OrdinalIgnoreCase)),
                PartialEmployees: rows.Count(x => string.Equals(x.OverallStatus, "Partial", StringComparison.OrdinalIgnoreCase)),
                IncompleteEmployees: rows.Count(x => string.Equals(x.OverallStatus, "Incomplete", StringComparison.OrdinalIgnoreCase)),
                ExpiringSoonDocuments: rows.Sum(x => x.ExpiringSoonCount));
        }

        public async Task<IReadOnlyList<DocumentChecklistItemDto>> GetChecklistAsync(int employeeId)
        {
            const string sql = @"
SELECT
    checklist_id,
    employee_id,
    COALESCE(position_name, '') AS position_name,
    COALESCE(employment_type, '') AS employment_type,
    document_code,
    document_name,
    document_tier,
    is_required,
    status,
    submitted_date,
    expiry_date,
    verified_date,
    verified_by,
    waived_reason,
    remarks,
    file_name,
    file_path,
    file_size,
    uploaded_at
FROM employee_document_checklist
WHERE employee_id = @employee_id
ORDER BY document_tier, checklist_id;";

            const string fallbackSql = @"
SELECT
    checklist_id,
    employee_id,
    COALESCE(position_name, '') AS position_name,
    COALESCE(employment_type, '') AS employment_type,
    document_code,
    document_name,
    document_tier,
    is_required,
    status,
    submitted_date,
    expiry_date,
    verified_date,
    verified_by,
    waived_reason,
    remarks
FROM employee_document_checklist
WHERE employee_id = @employee_id
ORDER BY document_tier, checklist_id;";

            var rows = new List<DocumentChecklistItemDto>();
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            try
            {
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@employee_id", employeeId);

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new DocumentChecklistItemDto(
                        ChecklistId: ToInt(reader["checklist_id"]),
                        EmployeeId: ToInt(reader["employee_id"]),
                        PositionName: reader["position_name"]?.ToString() ?? string.Empty,
                        EmploymentType: reader["employment_type"]?.ToString() ?? string.Empty,
                        DocumentCode: reader["document_code"]?.ToString() ?? string.Empty,
                        DocumentName: reader["document_name"]?.ToString() ?? string.Empty,
                        DocumentTier: ToInt(reader["document_tier"]),
                        IsRequired: ToInt(reader["is_required"]) > 0,
                        Status: reader["status"]?.ToString() ?? "not_submitted",
                        SubmittedDate: ToNullableDate(reader["submitted_date"]),
                        ExpiryDate: ToNullableDate(reader["expiry_date"]),
                        VerifiedDate: ToNullableDate(reader["verified_date"]),
                        VerifiedBy: ToNullableString(reader["verified_by"]),
                        WaivedReason: ToNullableString(reader["waived_reason"]),
                        Remarks: ToNullableString(reader["remarks"]),
                        FileName: ToNullableString(reader["file_name"]),
                        FilePath: ToNullableString(reader["file_path"]),
                        FileSize: ToLong(reader["file_size"]),
                        UploadedAt: ToNullableDateTime(reader["uploaded_at"])));
                }
            }
            catch (MySqlException ex) when (IsMissingChecklistStorageColumnError(ex))
            {
                await using var fallbackCommand = new MySqlCommand(fallbackSql, connection);
                fallbackCommand.Parameters.AddWithValue("@employee_id", employeeId);

                await using var reader = await fallbackCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new DocumentChecklistItemDto(
                        ChecklistId: ToInt(reader["checklist_id"]),
                        EmployeeId: ToInt(reader["employee_id"]),
                        PositionName: reader["position_name"]?.ToString() ?? string.Empty,
                        EmploymentType: reader["employment_type"]?.ToString() ?? string.Empty,
                        DocumentCode: reader["document_code"]?.ToString() ?? string.Empty,
                        DocumentName: reader["document_name"]?.ToString() ?? string.Empty,
                        DocumentTier: ToInt(reader["document_tier"]),
                        IsRequired: ToInt(reader["is_required"]) > 0,
                        Status: reader["status"]?.ToString() ?? "not_submitted",
                        SubmittedDate: ToNullableDate(reader["submitted_date"]),
                        ExpiryDate: ToNullableDate(reader["expiry_date"]),
                        VerifiedDate: ToNullableDate(reader["verified_date"]),
                        VerifiedBy: ToNullableString(reader["verified_by"]),
                        WaivedReason: ToNullableString(reader["waived_reason"]),
                        Remarks: ToNullableString(reader["remarks"]),
                        FileName: null,
                        FilePath: null,
                        FileSize: 0,
                        UploadedAt: null));
                }
            }

            return rows;
        }

        public async Task<IReadOnlyList<ExpiringDocumentDto>> GetExpiringDocumentsAsync(int daysAhead = 30)
        {
            const string sql = @"
SELECT
    c.checklist_id,
    c.employee_id,
    COALESCE(e.employee_no, '-') AS employee_no,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS employee_name,
    c.document_name,
    c.expiry_date,
    c.status
FROM employee_document_checklist c
INNER JOIN employees e ON e.employee_id = c.employee_id
WHERE c.expiry_date IS NOT NULL
  AND c.expiry_date BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL @days DAY)
  AND c.status <> 'waived'
ORDER BY c.expiry_date ASC, e.employee_no;";

            var rows = new List<ExpiringDocumentDto>();
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@days", Math.Max(1, daysAhead));

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var expiryDate = ToNullableDate(reader["expiry_date"]);
                if (!expiryDate.HasValue)
                {
                    continue;
                }

                rows.Add(new ExpiringDocumentDto(
                    ChecklistId: ToInt(reader["checklist_id"]),
                    EmployeeId: ToInt(reader["employee_id"]),
                    EmployeeNo: reader["employee_no"]?.ToString() ?? "-",
                    EmployeeName: reader["employee_name"]?.ToString() ?? "-",
                    DocumentName: reader["document_name"]?.ToString() ?? string.Empty,
                    ExpiryDate: expiryDate.Value,
                    Status: reader["status"]?.ToString() ?? "not_submitted"));
            }

            return rows;
        }

        public async Task UpdateDocumentStatusAsync(
            int checklistId,
            string status,
            DateTime? submittedDate,
            DateTime? expiryDate,
            DateTime? verifiedDate,
            string? verifiedBy,
            string? waivedReason,
            string? remarks)
        {
            if (checklistId <= 0)
            {
                throw new InvalidOperationException("Invalid checklist document.");
            }

            var normalizedStatus = NormalizeStatus(status);
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var documentCode = await GetDocumentCodeAsync(connection, checklistId);
            if (string.IsNullOrWhiteSpace(documentCode))
            {
                throw new InvalidOperationException("Checklist document was not found.");
            }

            if (normalizedStatus == "waived" && string.IsNullOrWhiteSpace(waivedReason))
            {
                throw new InvalidOperationException("Waived documents require a waived reason.");
            }

            var effectiveSubmittedDate = submittedDate;
            if (!effectiveSubmittedDate.HasValue && (normalizedStatus == "submitted" || normalizedStatus == "verified"))
            {
                effectiveSubmittedDate = DateTime.Today;
            }

            DateTime? effectiveVerifiedDate = normalizedStatus == "verified"
                ? verifiedDate ?? DateTime.Today
                : null;

            var effectiveVerifiedBy = normalizedStatus == "verified"
                ? (string.IsNullOrWhiteSpace(verifiedBy) ? null : verifiedBy.Trim())
                : null;

            var effectiveWaivedReason = normalizedStatus == "waived"
                ? waivedReason?.Trim()
                : null;

            var effectiveExpiryDate = expiryDate;
            if (!effectiveExpiryDate.HasValue &&
                effectiveSubmittedDate.HasValue &&
                DocumentChecklistDefinitions.TryGetDefinition(documentCode, out var definition) &&
                definition.ExpiryMonths.HasValue)
            {
                effectiveExpiryDate = effectiveSubmittedDate.Value.AddMonths(definition.ExpiryMonths.Value);
            }

            const string sql = @"
UPDATE employee_document_checklist
SET
    status = @status,
    submitted_date = @submitted_date,
    expiry_date = @expiry_date,
    verified_date = @verified_date,
    verified_by = @verified_by,
    waived_reason = @waived_reason,
    remarks = @remarks,
    expiry_alert_sent = CASE
        WHEN @expiry_date IS NULL THEN 0
        WHEN @expiry_date > CURDATE() THEN 0
        ELSE expiry_alert_sent
    END
WHERE checklist_id = @checklist_id;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@status", normalizedStatus);
            command.Parameters.AddWithValue("@submitted_date", DbValue(effectiveSubmittedDate));
            command.Parameters.AddWithValue("@expiry_date", DbValue(effectiveExpiryDate));
            command.Parameters.AddWithValue("@verified_date", DbValue(effectiveVerifiedDate));
            command.Parameters.AddWithValue("@verified_by", DbValue(effectiveVerifiedBy));
            command.Parameters.AddWithValue("@waived_reason", DbValue(effectiveWaivedReason));
            command.Parameters.AddWithValue("@remarks", DbValue(string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim()));
            command.Parameters.AddWithValue("@checklist_id", checklistId);

            if (await command.ExecuteNonQueryAsync() <= 0)
            {
                throw new InvalidOperationException("Checklist document was not updated.");
            }
        }

        public async Task AddChecklistAttachmentAsync(int checklistId, int employeeId, string filePath, int? uploadedByEmployeeId = null)
        {
            if (checklistId <= 0)
            {
                throw new InvalidOperationException("Select a required document before uploading.");
            }

            if (employeeId <= 0)
            {
                throw new InvalidOperationException("Unable to resolve your employee profile.");
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new InvalidOperationException("Select a file first.");
            }

            var normalizedPath = filePath.Trim();
            if (!File.Exists(normalizedPath))
            {
                throw new InvalidOperationException($"Selected file not found: {normalizedPath}");
            }

            byte[] fileBytes;
            try
            {
                fileBytes = await File.ReadAllBytesAsync(normalizedPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unable to read selected file: {ex.Message}");
            }

            if (fileBytes.Length == 0)
            {
                throw new InvalidOperationException("Selected file is empty.");
            }

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await EnsureChecklistStorageColumnsAsync(connection);

            var documentCode = await GetDocumentCodeAsync(connection, checklistId);
            if (string.IsNullOrWhiteSpace(documentCode))
            {
                throw new InvalidOperationException("Selected checklist document was not found.");
            }

            var submittedDate = DateTime.Today;
            DateTime? expiryDate = null;
            if (DocumentChecklistDefinitions.TryGetDefinition(documentCode, out var definition) &&
                definition.ExpiryMonths.HasValue)
            {
                expiryDate = submittedDate.AddMonths(definition.ExpiryMonths.Value);
            }

            const string sql = @"
UPDATE employee_document_checklist
SET
    status = 'submitted',
    submitted_date = @submitted_date,
    expiry_date = @expiry_date,
    verified_date = NULL,
    verified_by = NULL,
    waived_reason = NULL,
    file_name = @file_name,
    file_path = @file_path,
    file_blob = @file_blob,
    file_size = @file_size,
    uploaded_at = NOW(),
    uploaded_by_employee_id = @uploaded_by_employee_id
WHERE checklist_id = @checklist_id
  AND employee_id = @employee_id;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@submitted_date", submittedDate);
            command.Parameters.AddWithValue("@expiry_date", DbValue(expiryDate));
            command.Parameters.AddWithValue("@file_name", Path.GetFileName(normalizedPath));
            command.Parameters.AddWithValue("@file_path", normalizedPath);
            command.Parameters.AddWithValue("@file_blob", fileBytes);
            command.Parameters.AddWithValue("@file_size", fileBytes.LongLength);
            command.Parameters.AddWithValue(
                "@uploaded_by_employee_id",
                uploadedByEmployeeId.HasValue && uploadedByEmployeeId.Value > 0
                    ? uploadedByEmployeeId.Value
                    : employeeId);
            command.Parameters.AddWithValue("@checklist_id", checklistId);
            command.Parameters.AddWithValue("@employee_id", employeeId);

            if (await command.ExecuteNonQueryAsync() <= 0)
            {
                throw new InvalidOperationException("Required document was not uploaded.");
            }
        }

        public async Task<ChecklistAttachmentContentDto?> GetChecklistAttachmentAsync(int checklistId, int? employeeId = null)
        {
            if (checklistId <= 0)
            {
                return null;
            }

            const string sql = @"
SELECT
    COALESCE(file_name, '') AS file_name,
    COALESCE(file_path, '') AS file_path,
    file_blob,
    COALESCE(file_size, 0) AS file_size,
    uploaded_at
FROM employee_document_checklist
WHERE checklist_id = @checklist_id
  AND (@employee_id IS NULL OR employee_id = @employee_id)
LIMIT 1;";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@checklist_id", checklistId);
                command.Parameters.AddWithValue("@employee_id", employeeId.HasValue && employeeId.Value > 0 ? employeeId.Value : DBNull.Value);

                await using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return null;
                }

                var filePath = ToNullableString(reader["file_path"]);
                var fileBlob = reader["file_blob"] == DBNull.Value ? null : (byte[])reader["file_blob"];
                if (string.IsNullOrWhiteSpace(filePath) && (fileBlob == null || fileBlob.Length == 0))
                {
                    return null;
                }

                var fileName = ToNullableString(reader["file_name"]) ?? $"checklist_{checklistId}.bin";
                return new ChecklistAttachmentContentDto(
                    FileName: fileName,
                    FilePath: filePath,
                    FileBlob: fileBlob,
                    FileSize: ToLong(reader["file_size"]),
                    UploadedAt: ToNullableDateTime(reader["uploaded_at"]));
            }
            catch (MySqlException ex) when (IsMissingChecklistStorageColumnError(ex))
            {
                return null;
            }
        }

        private async Task<EmployeeChecklistProfile> GetEmployeeProfileAsync(MySqlConnection connection, MySqlTransaction? transaction, int employeeId)
        {
            const string sql = @"
SELECT
    e.employee_id,
    COALESCE(e.employee_no, '-') AS employee_no,
    CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')) AS employee_name,
    COALESCE(p.position_name, '') AS position_name,
    COALESCE(at.type_name, 'Permanent') AS employment_type
FROM employees e
LEFT JOIN positions p ON p.position_id = e.position_id
LEFT JOIN appointment_types at ON at.appointment_type_id = e.appointment_type_id
WHERE e.employee_id = @employee_id
LIMIT 1;";

            await using var command = transaction == null
                ? new MySqlCommand(sql, connection)
                : new MySqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@employee_id", employeeId);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("Employee was not found.");
            }

            return new EmployeeChecklistProfile(
                EmployeeId: ToInt(reader["employee_id"]),
                EmployeeNo: reader["employee_no"]?.ToString() ?? "-",
                EmployeeName: reader["employee_name"]?.ToString() ?? "-",
                PositionName: reader["position_name"]?.ToString() ?? string.Empty,
                EmploymentType: reader["employment_type"]?.ToString() ?? "Permanent");
        }

        private Task<EmployeeChecklistProfile> GetEmployeeProfileAsync(MySqlConnection connection, int employeeId) =>
            GetEmployeeProfileAsync(connection, transaction: null, employeeId);

        private async Task InsertChecklistRowsAsync(MySqlConnection connection, MySqlTransaction transaction, int employeeId, string? positionName, string? employmentType)
        {
            const string countSql = @"
SELECT COUNT(*)
FROM employee_document_checklist
WHERE employee_id = @employee_id;";

            await using (var countCommand = new MySqlCommand(countSql, connection, transaction))
            {
                countCommand.Parameters.AddWithValue("@employee_id", employeeId);
                var countObj = await countCommand.ExecuteScalarAsync();
                var count = countObj == null || countObj == DBNull.Value
                    ? 0
                    : Convert.ToInt32(countObj, CultureInfo.InvariantCulture);
                if (count > 0)
                {
                    return;
                }
            }

            var docs = DocumentChecklistDefinitions.GetDocumentsForEmployee(positionName, employmentType);
            const string insertSql = @"
INSERT INTO employee_document_checklist (
    employee_id,
    position_name,
    employment_type,
    document_code,
    document_name,
    document_tier,
    is_required,
    status
) VALUES (
    @employee_id,
    @position_name,
    @employment_type,
    @document_code,
    @document_name,
    @document_tier,
    @is_required,
    'not_submitted'
);";

            foreach (var doc in docs)
            {
                await using var insertCommand = new MySqlCommand(insertSql, connection, transaction);
                insertCommand.Parameters.AddWithValue("@employee_id", employeeId);
                insertCommand.Parameters.AddWithValue("@position_name", positionName ?? string.Empty);
                insertCommand.Parameters.AddWithValue("@employment_type", string.IsNullOrWhiteSpace(employmentType) ? "Permanent" : employmentType.Trim());
                insertCommand.Parameters.AddWithValue("@document_code", doc.Code);
                insertCommand.Parameters.AddWithValue("@document_name", doc.Name);
                insertCommand.Parameters.AddWithValue("@document_tier", doc.Tier);
                insertCommand.Parameters.AddWithValue("@is_required", 1);
                await insertCommand.ExecuteNonQueryAsync();
            }
        }

        private static string ResolveOverallStatus(int totalDocs, int verifiedDocs, int submittedDocs, int waivedDocs, int expiredDocs)
        {
            if (totalDocs <= 0)
            {
                return "Incomplete";
            }

            if (verifiedDocs + waivedDocs >= totalDocs)
            {
                return "Complete";
            }

            if (verifiedDocs + submittedDocs + waivedDocs + expiredDocs > 0)
            {
                return "Partial";
            }

            return "Incomplete";
        }

        private static int StatusRank(string? status) =>
            status?.Trim().ToUpperInvariant() switch
            {
                "INCOMPLETE" => 0,
                "PARTIAL" => 1,
                "COMPLETE" => 2,
                _ => 3
            };

        private static async Task<string?> GetDocumentCodeAsync(MySqlConnection connection, int checklistId)
        {
            const string sql = @"
SELECT document_code
FROM employee_document_checklist
WHERE checklist_id = @checklist_id
LIMIT 1;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@checklist_id", checklistId);
            var value = await command.ExecuteScalarAsync();
            return value?.ToString();
        }

        private static async Task EnsureChecklistStorageColumnsAsync(MySqlConnection connection)
        {
            await EnsureColumnAsync(
                connection,
                "file_name",
                "ALTER TABLE employee_document_checklist ADD COLUMN file_name VARCHAR(255) NULL AFTER remarks;");
            await EnsureColumnAsync(
                connection,
                "file_path",
                "ALTER TABLE employee_document_checklist ADD COLUMN file_path VARCHAR(500) NULL AFTER file_name;");
            await EnsureColumnAsync(
                connection,
                "file_blob",
                "ALTER TABLE employee_document_checklist ADD COLUMN file_blob LONGBLOB NULL AFTER file_path;");
            await EnsureColumnAsync(
                connection,
                "file_size",
                "ALTER TABLE employee_document_checklist ADD COLUMN file_size BIGINT NULL AFTER file_blob;");
            await EnsureColumnAsync(
                connection,
                "uploaded_at",
                "ALTER TABLE employee_document_checklist ADD COLUMN uploaded_at DATETIME NULL AFTER file_size;");
            await EnsureColumnAsync(
                connection,
                "uploaded_by_employee_id",
                "ALTER TABLE employee_document_checklist ADD COLUMN uploaded_by_employee_id INT NULL AFTER uploaded_at;");
        }

        private static async Task EnsureColumnAsync(MySqlConnection connection, string columnName, string alterSql)
        {
            const string existsSql = @"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'employee_document_checklist'
  AND COLUMN_NAME = @column_name;";

            await using var existsCommand = new MySqlCommand(existsSql, connection);
            existsCommand.Parameters.AddWithValue("@column_name", columnName);
            var exists = Convert.ToInt32(await existsCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture) > 0;
            if (exists)
            {
                return;
            }

            await using var alterCommand = new MySqlCommand(alterSql, connection);
            await alterCommand.ExecuteNonQueryAsync();
        }

        private static string NormalizeStatus(string? status)
        {
            var normalized = status?.Trim().ToLowerInvariant();
            return normalized switch
            {
                "submitted" => "submitted",
                "verified" => "verified",
                "expired" => "expired",
                "waived" => "waived",
                _ => "not_submitted"
            };
        }

        private static object DbValue(DateTime? value) => value.HasValue ? value.Value.Date : DBNull.Value;
        private static object DbValue(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
        private static int ToInt(object? value) => value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        private static long ToLong(object? value) => value == null || value == DBNull.Value ? 0L : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        private static DateTime? ToNullableDate(object? value) => value == null || value == DBNull.Value ? null : Convert.ToDateTime(value, CultureInfo.InvariantCulture);
        private static DateTime? ToNullableDateTime(object? value) => value == null || value == DBNull.Value ? null : Convert.ToDateTime(value, CultureInfo.InvariantCulture);
        private static string? ToNullableString(object? value)
        {
            var text = value?.ToString();
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        private static bool IsMissingChecklistStorageColumnError(MySqlException ex) =>
            ex.Number == 1054 &&
            (ex.Message.Contains("file_name", StringComparison.OrdinalIgnoreCase) ||
             ex.Message.Contains("file_path", StringComparison.OrdinalIgnoreCase) ||
             ex.Message.Contains("file_blob", StringComparison.OrdinalIgnoreCase) ||
             ex.Message.Contains("file_size", StringComparison.OrdinalIgnoreCase) ||
             ex.Message.Contains("uploaded_at", StringComparison.OrdinalIgnoreCase) ||
             ex.Message.Contains("uploaded_by_employee_id", StringComparison.OrdinalIgnoreCase));
    }
}
