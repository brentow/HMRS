
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record RecruitmentStatsDto(
        int TotalPostings,
        int OpenPostings,
        int TotalApplicants,
        int TotalApplications,
        int ScheduledInterviews,
        int PendingOffers);

    public record RecruitmentDepartmentOptionDto(int DepartmentId, string Name);
    public record RecruitmentPositionOptionDto(int PositionId, int? DepartmentId, string Name);
    public record RecruitmentEmployeeOptionDto(int EmployeeId, string EmployeeNo, string EmployeeName);
    public record RecruitmentPostingOptionDto(long JobPostingId, string PostingCode, string Title, string Status);
    public record RecruitmentApplicantOptionDto(long ApplicantId, string ApplicantNo, string ApplicantName);
    public record RecruitmentApplicationOptionDto(long JobApplicationId, long ApplicantId, string ApplicantName, long JobPostingId, string PostingCode, string PostingTitle, string Status);

    public record RecruitmentJobPostingDto(
        long JobPostingId,
        string PostingCode,
        string Title,
        int? DepartmentId,
        string DepartmentName,
        int? PositionId,
        string PositionName,
        string EmploymentType,
        int Vacancies,
        string Status,
        DateTime OpenDate,
        DateTime? CloseDate,
        int ApplicationCount);

    public record RecruitmentApplicantDto(
        long ApplicantId,
        string ApplicantNo,
        string FirstName,
        string LastName,
        string MiddleName,
        string FullName,
        string Email,
        string MobileNo,
        string Address,
        DateTime? BirthDate,
        DateTime CreatedAt,
        int ApplicationCount);

    public record RecruitmentApplicationDto(
        long JobApplicationId,
        long ApplicantId,
        string ApplicantNo,
        string ApplicantName,
        long JobPostingId,
        string PostingCode,
        string PostingTitle,
        string DepartmentName,
        DateTime AppliedAt,
        string Status,
        string Notes,
        int InterviewCount,
        int OfferCount);

    public record RecruitmentInterviewDto(
        long InterviewScheduleId,
        long JobApplicationId,
        string ApplicantName,
        string PostingCode,
        string PostingTitle,
        DateTime InterviewDateTime,
        string InterviewType,
        string Location,
        int? InterviewerEmployeeId,
        string InterviewerName,
        string Status,
        string Remarks);

    public record RecruitmentOfferDto(
        long JobOfferId,
        long JobApplicationId,
        string ApplicantName,
        string PostingCode,
        string PostingTitle,
        DateTime OfferedAt,
        string OfferStatus,
        decimal? SalaryOffer,
        DateTime? StartDate,
        string Remarks);

    public class RecruitmentDataService
    {
        private readonly string _connectionString;

        public RecruitmentDataService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<RecruitmentStatsDto> GetStatsAsync()
        {
            const string sql = @"
SELECT
    (SELECT COUNT(*) FROM job_postings) AS total_postings,
    (SELECT COUNT(*) FROM job_postings WHERE status = 'OPEN') AS open_postings,
    (SELECT COUNT(*) FROM applicants) AS total_applicants,
    (SELECT COUNT(*) FROM job_applications) AS total_applications,
    (SELECT COUNT(*) FROM interview_schedules WHERE status = 'SCHEDULED') AS scheduled_interviews,
    (SELECT COUNT(*) FROM job_offers WHERE offer_status = 'PENDING') AS pending_offers;";

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return new RecruitmentStatsDto(0, 0, 0, 0, 0, 0);
                }

                return new RecruitmentStatsDto(
                    ToInt(reader["total_postings"]),
                    ToInt(reader["open_postings"]),
                    ToInt(reader["total_applicants"]),
                    ToInt(reader["total_applications"]),
                    ToInt(reader["scheduled_interviews"]),
                    ToInt(reader["pending_offers"]));
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return new RecruitmentStatsDto(0, 0, 0, 0, 0, 0);
            }
        }

        public async Task<IReadOnlyList<RecruitmentDepartmentOptionDto>> GetDepartmentsAsync()
        {
            const string sql = "SELECT department_id, dept_name FROM departments ORDER BY dept_name;";
            var rows = new List<RecruitmentDepartmentOptionDto>();

            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new RecruitmentDepartmentOptionDto(ToInt(reader["department_id"]), reader["dept_name"]?.ToString() ?? "-"));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<RecruitmentDepartmentOptionDto>();
            }

            return rows;
        }

        public async Task<IReadOnlyList<RecruitmentPositionOptionDto>> GetPositionsAsync(int? departmentId = null)
        {
            const string sql = @"
SELECT position_id, department_id, position_name
FROM positions
WHERE (@department_id IS NULL OR department_id = @department_id)
ORDER BY position_name;";

            var rows = new List<RecruitmentPositionOptionDto>();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@department_id", departmentId.HasValue && departmentId.Value > 0 ? departmentId.Value : DBNull.Value);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new RecruitmentPositionOptionDto(
                        ToInt(reader["position_id"]),
                        reader["department_id"] == DBNull.Value ? null : ToInt(reader["department_id"]),
                        reader["position_name"]?.ToString() ?? "-"));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<RecruitmentPositionOptionDto>();
            }

            return rows;
        }

        public async Task<IReadOnlyList<RecruitmentEmployeeOptionDto>> GetEmployeesAsync()
        {
            const string sql = @"
SELECT
    employee_id,
    employee_no,
    CONCAT(last_name, ', ', first_name, IFNULL(CONCAT(' ', middle_name), '')) AS employee_name
FROM employees
WHERE status = 'ACTIVE'
ORDER BY employee_no;";

            var rows = new List<RecruitmentEmployeeOptionDto>();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new RecruitmentEmployeeOptionDto(
                        ToInt(reader["employee_id"]),
                        reader["employee_no"]?.ToString() ?? "-",
                        reader["employee_name"]?.ToString() ?? "-"));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<RecruitmentEmployeeOptionDto>();
            }

            return rows;
        }

        public async Task<IReadOnlyList<RecruitmentPostingOptionDto>> GetPostingOptionsAsync(bool onlyOpen = false)
        {
            const string sql = @"
SELECT job_posting_id, posting_code, title, status
FROM job_postings
WHERE (@only_open = 0 OR status = 'OPEN')
ORDER BY open_date DESC, job_posting_id DESC;";

            var rows = new List<RecruitmentPostingOptionDto>();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@only_open", onlyOpen ? 1 : 0);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new RecruitmentPostingOptionDto(
                        ToLong(reader["job_posting_id"]),
                        reader["posting_code"]?.ToString() ?? "-",
                        reader["title"]?.ToString() ?? "-",
                        reader["status"]?.ToString() ?? "OPEN"));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<RecruitmentPostingOptionDto>();
            }

            return rows;
        }

        public async Task<IReadOnlyList<RecruitmentApplicantOptionDto>> GetApplicantOptionsAsync()
        {
            const string sql = @"
SELECT
    applicant_id,
    applicant_no,
    CONCAT(last_name, ', ', first_name, IFNULL(CONCAT(' ', middle_name), '')) AS applicant_name
FROM applicants
ORDER BY last_name, first_name;";

            var rows = new List<RecruitmentApplicantOptionDto>();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new RecruitmentApplicantOptionDto(
                        ToLong(reader["applicant_id"]),
                        reader["applicant_no"]?.ToString() ?? "-",
                        reader["applicant_name"]?.ToString() ?? "-"));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<RecruitmentApplicantOptionDto>();
            }

            return rows;
        }

        public async Task<IReadOnlyList<RecruitmentApplicationOptionDto>> GetApplicationOptionsAsync(bool onlyActivePipeline = false)
        {
            const string sql = @"
SELECT
    ja.job_application_id,
    ja.applicant_id,
    CONCAT(a.last_name, ', ', a.first_name, IFNULL(CONCAT(' ', a.middle_name), '')) AS applicant_name,
    ja.job_posting_id,
    COALESCE(jp.posting_code, '-') AS posting_code,
    COALESCE(jp.title, '-') AS posting_title,
    COALESCE(ja.status, 'SUBMITTED') AS status
FROM job_applications ja
JOIN applicants a ON a.applicant_id = ja.applicant_id
JOIN job_postings jp ON jp.job_posting_id = ja.job_posting_id
WHERE (@only_active = 0 OR ja.status NOT IN ('HIRED', 'REJECTED', 'WITHDRAWN'))
ORDER BY ja.applied_at DESC, ja.job_application_id DESC;";

            var rows = new List<RecruitmentApplicationOptionDto>();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@only_active", onlyActivePipeline ? 1 : 0);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new RecruitmentApplicationOptionDto(
                        ToLong(reader["job_application_id"]),
                        ToLong(reader["applicant_id"]),
                        reader["applicant_name"]?.ToString() ?? "-",
                        ToLong(reader["job_posting_id"]),
                        reader["posting_code"]?.ToString() ?? "-",
                        reader["posting_title"]?.ToString() ?? "-",
                        reader["status"]?.ToString() ?? "SUBMITTED"));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<RecruitmentApplicationOptionDto>();
            }

            return rows;
        }

        public async Task<IReadOnlyList<RecruitmentJobPostingDto>> GetJobPostingsAsync(string? search = null, string? statusFilter = null, int limit = 400)
        {
            const string sql = @"
SELECT
    jp.job_posting_id,
    jp.posting_code,
    jp.title,
    jp.department_id,
    COALESCE(d.dept_name, '-') AS department_name,
    jp.position_id,
    COALESCE(p.position_name, '-') AS position_name,
    COALESCE(jp.employment_type, 'CASUAL') AS employment_type,
    COALESCE(jp.vacancies, 0) AS vacancies,
    COALESCE(jp.status, 'OPEN') AS status,
    jp.open_date,
    jp.close_date,
    COALESCE(app.app_count, 0) AS app_count
FROM job_postings jp
LEFT JOIN departments d ON d.department_id = jp.department_id
LEFT JOIN positions p ON p.position_id = jp.position_id
LEFT JOIN (
    SELECT job_posting_id, COUNT(*) AS app_count
    FROM job_applications
    GROUP BY job_posting_id
) app ON app.job_posting_id = jp.job_posting_id
WHERE (@status IS NULL OR jp.status = @status)
  AND (
      @search = '' OR
      jp.posting_code LIKE CONCAT('%', @search, '%') OR
      jp.title LIKE CONCAT('%', @search, '%') OR
      COALESCE(d.dept_name, '') LIKE CONCAT('%', @search, '%') OR
      COALESCE(p.position_name, '') LIKE CONCAT('%', @search, '%')
  )
ORDER BY jp.open_date DESC, jp.job_posting_id DESC
LIMIT @limit;";

            var rows = new List<RecruitmentJobPostingDto>();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@status", NormalizeFilter(statusFilter));
                command.Parameters.AddWithValue("@search", string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim());
                command.Parameters.AddWithValue("@limit", Math.Max(1, limit));
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new RecruitmentJobPostingDto(
                        ToLong(reader["job_posting_id"]),
                        reader["posting_code"]?.ToString() ?? "-",
                        reader["title"]?.ToString() ?? "-",
                        reader["department_id"] == DBNull.Value ? null : ToInt(reader["department_id"]),
                        reader["department_name"]?.ToString() ?? "-",
                        reader["position_id"] == DBNull.Value ? null : ToInt(reader["position_id"]),
                        reader["position_name"]?.ToString() ?? "-",
                        reader["employment_type"]?.ToString() ?? "CASUAL",
                        ToInt(reader["vacancies"]),
                        reader["status"]?.ToString() ?? "OPEN",
                        ToDate(reader["open_date"]),
                        reader["close_date"] == DBNull.Value ? null : ToDate(reader["close_date"]),
                        ToInt(reader["app_count"])));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<RecruitmentJobPostingDto>();
            }

            return rows;
        }

        public async Task<long> AddJobPostingAsync(string postingCode, string title, int? departmentId, int? positionId, string? employmentType, int vacancies, DateTime openDate, DateTime? closeDate, string? status)
        {
            if (string.IsNullOrWhiteSpace(postingCode) || string.IsNullOrWhiteSpace(title))
            {
                throw new InvalidOperationException("Posting code and title are required.");
            }

            const string sql = @"
INSERT INTO job_postings
    (posting_code, title, department_id, position_id, employment_type, vacancies, status, open_date, close_date)
VALUES
    (@posting_code, @title, @department_id, @position_id, @employment_type, @vacancies, @status, @open_date, @close_date);
SELECT LAST_INSERT_ID();";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@posting_code", postingCode.Trim());
            command.Parameters.AddWithValue("@title", title.Trim());
            command.Parameters.AddWithValue("@department_id", departmentId.HasValue && departmentId.Value > 0 ? departmentId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@position_id", positionId.HasValue && positionId.Value > 0 ? positionId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@employment_type", NormalizeEmploymentType(employmentType));
            command.Parameters.AddWithValue("@vacancies", Math.Max(1, vacancies));
            command.Parameters.AddWithValue("@status", NormalizePostingStatus(status));
            command.Parameters.AddWithValue("@open_date", openDate.Date);
            command.Parameters.AddWithValue("@close_date", closeDate.HasValue ? closeDate.Value.Date : DBNull.Value);
            try
            {
                var result = await command.ExecuteScalarAsync();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt64(result, CultureInfo.InvariantCulture);
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                throw new InvalidOperationException("Posting code already exists.");
            }
        }

        public async Task UpdateJobPostingAsync(long jobPostingId, int? departmentId, int? positionId, string? employmentType, int vacancies, string? status, DateTime openDate, DateTime? closeDate)
        {
            if (jobPostingId <= 0)
            {
                throw new InvalidOperationException("Invalid job posting.");
            }

            const string sql = @"
UPDATE job_postings
SET
    department_id = @department_id,
    position_id = @position_id,
    employment_type = @employment_type,
    vacancies = @vacancies,
    status = @status,
    open_date = @open_date,
    close_date = @close_date,
    updated_at = CURRENT_TIMESTAMP
WHERE job_posting_id = @job_posting_id;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@job_posting_id", jobPostingId);
            command.Parameters.AddWithValue("@department_id", departmentId.HasValue && departmentId.Value > 0 ? departmentId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@position_id", positionId.HasValue && positionId.Value > 0 ? positionId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@employment_type", NormalizeEmploymentType(employmentType));
            command.Parameters.AddWithValue("@vacancies", Math.Max(1, vacancies));
            command.Parameters.AddWithValue("@status", NormalizePostingStatus(status));
            command.Parameters.AddWithValue("@open_date", openDate.Date);
            command.Parameters.AddWithValue("@close_date", closeDate.HasValue ? closeDate.Value.Date : DBNull.Value);
            var affected = await command.ExecuteNonQueryAsync();
            if (affected == 0)
            {
                throw new InvalidOperationException("Job posting not found.");
            }
        }

        public async Task DeleteJobPostingAsync(long jobPostingId)
        {
            if (jobPostingId <= 0)
            {
                throw new InvalidOperationException("Invalid job posting.");
            }

            const string sql = "DELETE FROM job_postings WHERE job_posting_id = @job_posting_id;";
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@job_posting_id", jobPostingId);
            try
            {
                var affected = await command.ExecuteNonQueryAsync();
                if (affected == 0)
                {
                    throw new InvalidOperationException("Job posting not found.");
                }
            }
            catch (MySqlException ex) when (ex.Number == 1451)
            {
                throw new InvalidOperationException("Cannot delete posting with existing applications.");
            }
        }

        public async Task<IReadOnlyList<RecruitmentApplicantDto>> GetApplicantsAsync(string? search = null, int limit = 500)
        {
            const string sql = @"
SELECT
    a.applicant_id,
    a.applicant_no,
    COALESCE(a.first_name, '') AS first_name,
    COALESCE(a.last_name, '') AS last_name,
    COALESCE(a.middle_name, '') AS middle_name,
    CONCAT(a.last_name, ', ', a.first_name, IFNULL(CONCAT(' ', a.middle_name), '')) AS full_name,
    COALESCE(a.email, '') AS email,
    COALESCE(a.mobile_no, '') AS mobile_no,
    COALESCE(a.address, '') AS address,
    a.birth_date,
    a.created_at,
    COALESCE(app.app_count, 0) AS app_count
FROM applicants a
LEFT JOIN (
    SELECT applicant_id, COUNT(*) AS app_count
    FROM job_applications
    GROUP BY applicant_id
) app ON app.applicant_id = a.applicant_id
WHERE (
    @search = '' OR
    a.applicant_no LIKE CONCAT('%', @search, '%') OR
    CONCAT(a.last_name, ', ', a.first_name, IFNULL(CONCAT(' ', a.middle_name), '')) LIKE CONCAT('%', @search, '%') OR
    COALESCE(a.email, '') LIKE CONCAT('%', @search, '%')
)
ORDER BY a.created_at DESC, a.applicant_id DESC
LIMIT @limit;";

            var rows = new List<RecruitmentApplicantDto>();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@search", string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim());
                command.Parameters.AddWithValue("@limit", Math.Max(1, limit));
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new RecruitmentApplicantDto(
                        ToLong(reader["applicant_id"]),
                        reader["applicant_no"]?.ToString() ?? "-",
                        reader["first_name"]?.ToString() ?? string.Empty,
                        reader["last_name"]?.ToString() ?? string.Empty,
                        reader["middle_name"]?.ToString() ?? string.Empty,
                        reader["full_name"]?.ToString() ?? "-",
                        reader["email"]?.ToString() ?? string.Empty,
                        reader["mobile_no"]?.ToString() ?? string.Empty,
                        reader["address"]?.ToString() ?? string.Empty,
                        reader["birth_date"] == DBNull.Value ? null : ToDate(reader["birth_date"]),
                        reader["created_at"] == DBNull.Value ? DateTime.Today : ToDateTime(reader["created_at"]),
                        ToInt(reader["app_count"])));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<RecruitmentApplicantDto>();
            }

            return rows;
        }

        public async Task<long> AddApplicantAsync(string applicantNo, string firstName, string lastName, string? middleName, string? email, string? mobileNo, string? address, DateTime? birthDate)
        {
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            {
                throw new InvalidOperationException("Applicant first name and last name are required.");
            }

            var finalApplicantNo = string.IsNullOrWhiteSpace(applicantNo)
                ? $"AP-{DateTime.Now:yyyyMMddHHmmss}"
                : applicantNo.Trim();

            const string sql = @"
INSERT INTO applicants
    (applicant_no, last_name, first_name, middle_name, email, mobile_no, address, birth_date)
VALUES
    (@applicant_no, @last_name, @first_name, @middle_name, @email, @mobile_no, @address, @birth_date);
SELECT LAST_INSERT_ID();";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@applicant_no", finalApplicantNo);
            command.Parameters.AddWithValue("@last_name", lastName.Trim());
            command.Parameters.AddWithValue("@first_name", firstName.Trim());
            command.Parameters.AddWithValue("@middle_name", string.IsNullOrWhiteSpace(middleName) ? DBNull.Value : middleName.Trim());
            command.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(email) ? DBNull.Value : email.Trim());
            command.Parameters.AddWithValue("@mobile_no", string.IsNullOrWhiteSpace(mobileNo) ? DBNull.Value : mobileNo.Trim());
            command.Parameters.AddWithValue("@address", string.IsNullOrWhiteSpace(address) ? DBNull.Value : address.Trim());
            command.Parameters.AddWithValue("@birth_date", birthDate.HasValue ? birthDate.Value.Date : DBNull.Value);
            try
            {
                var result = await command.ExecuteScalarAsync();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt64(result, CultureInfo.InvariantCulture);
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                throw new InvalidOperationException("Applicant number already exists.");
            }
        }

        public async Task UpdateApplicantAsync(long applicantId, string firstName, string lastName, string? middleName, string? email, string? mobileNo, string? address, DateTime? birthDate)
        {
            if (applicantId <= 0)
            {
                throw new InvalidOperationException("Invalid applicant.");
            }

            const string sql = @"
UPDATE applicants
SET
    first_name = @first_name,
    last_name = @last_name,
    middle_name = @middle_name,
    email = @email,
    mobile_no = @mobile_no,
    address = @address,
    birth_date = @birth_date,
    updated_at = CURRENT_TIMESTAMP
WHERE applicant_id = @applicant_id;";

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@applicant_id", applicantId);
            command.Parameters.AddWithValue("@first_name", firstName.Trim());
            command.Parameters.AddWithValue("@last_name", lastName.Trim());
            command.Parameters.AddWithValue("@middle_name", string.IsNullOrWhiteSpace(middleName) ? DBNull.Value : middleName.Trim());
            command.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(email) ? DBNull.Value : email.Trim());
            command.Parameters.AddWithValue("@mobile_no", string.IsNullOrWhiteSpace(mobileNo) ? DBNull.Value : mobileNo.Trim());
            command.Parameters.AddWithValue("@address", string.IsNullOrWhiteSpace(address) ? DBNull.Value : address.Trim());
            command.Parameters.AddWithValue("@birth_date", birthDate.HasValue ? birthDate.Value.Date : DBNull.Value);
            var affected = await command.ExecuteNonQueryAsync();
            if (affected == 0)
            {
                throw new InvalidOperationException("Applicant not found.");
            }
        }

        public async Task DeleteApplicantAsync(long applicantId)
        {
            if (applicantId <= 0)
            {
                throw new InvalidOperationException("Invalid applicant.");
            }

            const string sql = "DELETE FROM applicants WHERE applicant_id = @applicant_id;";
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@applicant_id", applicantId);
            try
            {
                var affected = await command.ExecuteNonQueryAsync();
                if (affected == 0)
                {
                    throw new InvalidOperationException("Applicant not found.");
                }
            }
            catch (MySqlException ex) when (ex.Number == 1451)
            {
                throw new InvalidOperationException("Cannot delete applicant with existing applications.");
            }
        }

        public async Task<IReadOnlyList<RecruitmentApplicationDto>> GetApplicationsAsync(string? search = null, string? statusFilter = null, int limit = 500)
        {
            const string sql = @"
SELECT
    ja.job_application_id,
    ja.applicant_id,
    COALESCE(a.applicant_no, '-') AS applicant_no,
    CONCAT(a.last_name, ', ', a.first_name, IFNULL(CONCAT(' ', a.middle_name), '')) AS applicant_name,
    ja.job_posting_id,
    COALESCE(jp.posting_code, '-') AS posting_code,
    COALESCE(jp.title, '-') AS posting_title,
    COALESCE(d.dept_name, '-') AS department_name,
    ja.applied_at,
    COALESCE(ja.status, 'SUBMITTED') AS status,
    COALESCE(ja.notes, '') AS notes,
    COALESCE(iv.iv_count, 0) AS iv_count,
    COALESCE(ofr.of_count, 0) AS of_count
FROM job_applications ja
JOIN applicants a ON a.applicant_id = ja.applicant_id
JOIN job_postings jp ON jp.job_posting_id = ja.job_posting_id
LEFT JOIN departments d ON d.department_id = jp.department_id
LEFT JOIN (SELECT job_application_id, COUNT(*) AS iv_count FROM interview_schedules GROUP BY job_application_id) iv ON iv.job_application_id = ja.job_application_id
LEFT JOIN (SELECT job_application_id, COUNT(*) AS of_count FROM job_offers GROUP BY job_application_id) ofr ON ofr.job_application_id = ja.job_application_id
WHERE (@status IS NULL OR ja.status = @status)
  AND (
      @search = '' OR
      COALESCE(a.applicant_no, '') LIKE CONCAT('%', @search, '%') OR
      CONCAT(a.last_name, ', ', a.first_name, IFNULL(CONCAT(' ', a.middle_name), '')) LIKE CONCAT('%', @search, '%') OR
      COALESCE(jp.posting_code, '') LIKE CONCAT('%', @search, '%') OR
      COALESCE(jp.title, '') LIKE CONCAT('%', @search, '%')
  )
ORDER BY ja.applied_at DESC, ja.job_application_id DESC
LIMIT @limit;";

            var rows = new List<RecruitmentApplicationDto>();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@status", NormalizeFilter(statusFilter));
                command.Parameters.AddWithValue("@search", string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim());
                command.Parameters.AddWithValue("@limit", Math.Max(1, limit));
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new RecruitmentApplicationDto(
                        ToLong(reader["job_application_id"]),
                        ToLong(reader["applicant_id"]),
                        reader["applicant_no"]?.ToString() ?? "-",
                        reader["applicant_name"]?.ToString() ?? "-",
                        ToLong(reader["job_posting_id"]),
                        reader["posting_code"]?.ToString() ?? "-",
                        reader["posting_title"]?.ToString() ?? "-",
                        reader["department_name"]?.ToString() ?? "-",
                        reader["applied_at"] == DBNull.Value ? DateTime.Today : ToDateTime(reader["applied_at"]),
                        reader["status"]?.ToString() ?? "SUBMITTED",
                        reader["notes"]?.ToString() ?? string.Empty,
                        ToInt(reader["iv_count"]),
                        ToInt(reader["of_count"])));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<RecruitmentApplicationDto>();
            }

            return rows;
        }

        public async Task<long> AddApplicationAsync(long applicantId, long postingId, string? status, string? notes)
        {
            const string sql = @"INSERT INTO job_applications (applicant_id, job_posting_id, status, notes) VALUES (@applicant_id, @job_posting_id, @status, @notes); SELECT LAST_INSERT_ID();";
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@applicant_id", applicantId);
            command.Parameters.AddWithValue("@job_posting_id", postingId);
            command.Parameters.AddWithValue("@status", NormalizeApplicationStatus(status));
            command.Parameters.AddWithValue("@notes", string.IsNullOrWhiteSpace(notes) ? DBNull.Value : notes.Trim());
            try
            {
                var result = await command.ExecuteScalarAsync();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt64(result, CultureInfo.InvariantCulture);
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                throw new InvalidOperationException("Applicant is already linked to this posting.");
            }
        }

        public async Task UpdateApplicationAsync(long jobApplicationId, string? status, string? notes)
        {
            const string sql = @"UPDATE job_applications SET status = @status, notes = @notes WHERE job_application_id = @job_application_id;";
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@job_application_id", jobApplicationId);
            command.Parameters.AddWithValue("@status", NormalizeApplicationStatus(status));
            command.Parameters.AddWithValue("@notes", string.IsNullOrWhiteSpace(notes) ? DBNull.Value : notes.Trim());
            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteApplicationAsync(long jobApplicationId)
        {
            const string sql = "DELETE FROM job_applications WHERE job_application_id = @job_application_id;";
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@job_application_id", jobApplicationId);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<IReadOnlyList<RecruitmentInterviewDto>> GetInterviewsAsync(string? search = null, string? statusFilter = null, int limit = 500)
        {
            const string sql = @"
SELECT
    i.interview_schedule_id,
    i.job_application_id,
    CONCAT(a.last_name, ', ', a.first_name, IFNULL(CONCAT(' ', a.middle_name), '')) AS applicant_name,
    COALESCE(jp.posting_code, '-') AS posting_code,
    COALESCE(jp.title, '-') AS posting_title,
    i.interview_datetime,
    COALESCE(i.interview_type, 'ONSITE') AS interview_type,
    COALESCE(i.location, '') AS location,
    i.interviewer_employee_id,
    COALESCE(CONCAT(e.last_name, ', ', e.first_name, IFNULL(CONCAT(' ', e.middle_name), '')), '-') AS interviewer_name,
    COALESCE(i.status, 'SCHEDULED') AS status,
    COALESCE(i.remarks, '') AS remarks
FROM interview_schedules i
JOIN job_applications ja ON ja.job_application_id = i.job_application_id
JOIN applicants a ON a.applicant_id = ja.applicant_id
JOIN job_postings jp ON jp.job_posting_id = ja.job_posting_id
LEFT JOIN employees e ON e.employee_id = i.interviewer_employee_id
WHERE (@status IS NULL OR i.status = @status)
  AND (@search = '' OR CONCAT(a.last_name, ', ', a.first_name, IFNULL(CONCAT(' ', a.middle_name), '')) LIKE CONCAT('%', @search, '%') OR COALESCE(jp.posting_code, '') LIKE CONCAT('%', @search, '%'))
ORDER BY i.interview_datetime DESC
LIMIT @limit;";

            var rows = new List<RecruitmentInterviewDto>();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@status", NormalizeFilter(statusFilter));
                command.Parameters.AddWithValue("@search", string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim());
                command.Parameters.AddWithValue("@limit", Math.Max(1, limit));
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new RecruitmentInterviewDto(
                        ToLong(reader["interview_schedule_id"]),
                        ToLong(reader["job_application_id"]),
                        reader["applicant_name"]?.ToString() ?? "-",
                        reader["posting_code"]?.ToString() ?? "-",
                        reader["posting_title"]?.ToString() ?? "-",
                        reader["interview_datetime"] == DBNull.Value ? DateTime.Today : ToDateTime(reader["interview_datetime"]),
                        reader["interview_type"]?.ToString() ?? "ONSITE",
                        reader["location"]?.ToString() ?? string.Empty,
                        reader["interviewer_employee_id"] == DBNull.Value ? null : ToInt(reader["interviewer_employee_id"]),
                        reader["interviewer_name"]?.ToString() ?? "-",
                        reader["status"]?.ToString() ?? "SCHEDULED",
                        reader["remarks"]?.ToString() ?? string.Empty));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<RecruitmentInterviewDto>();
            }

            return rows;
        }

        public async Task<long> AddInterviewAsync(long jobApplicationId, DateTime interviewDateTime, string? interviewType, string? location, int? interviewerEmployeeId, string? status, string? remarks)
        {
            const string sql = @"INSERT INTO interview_schedules (job_application_id, interview_datetime, interview_type, location, interviewer_employee_id, status, remarks) VALUES (@job_application_id, @interview_datetime, @interview_type, @location, @interviewer_employee_id, @status, @remarks); SELECT LAST_INSERT_ID();";
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@job_application_id", jobApplicationId);
            command.Parameters.AddWithValue("@interview_datetime", interviewDateTime);
            command.Parameters.AddWithValue("@interview_type", NormalizeInterviewType(interviewType));
            command.Parameters.AddWithValue("@location", string.IsNullOrWhiteSpace(location) ? DBNull.Value : location.Trim());
            command.Parameters.AddWithValue("@interviewer_employee_id", interviewerEmployeeId.HasValue && interviewerEmployeeId.Value > 0 ? interviewerEmployeeId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@status", NormalizeInterviewStatus(status));
            command.Parameters.AddWithValue("@remarks", string.IsNullOrWhiteSpace(remarks) ? DBNull.Value : remarks.Trim());
            var result = await command.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }

        public async Task UpdateInterviewAsync(long interviewScheduleId, DateTime interviewDateTime, string? interviewType, string? location, int? interviewerEmployeeId, string? status, string? remarks)
        {
            const string sql = @"UPDATE interview_schedules SET interview_datetime = @interview_datetime, interview_type = @interview_type, location = @location, interviewer_employee_id = @interviewer_employee_id, status = @status, remarks = @remarks WHERE interview_schedule_id = @interview_schedule_id;";
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@interview_schedule_id", interviewScheduleId);
            command.Parameters.AddWithValue("@interview_datetime", interviewDateTime);
            command.Parameters.AddWithValue("@interview_type", NormalizeInterviewType(interviewType));
            command.Parameters.AddWithValue("@location", string.IsNullOrWhiteSpace(location) ? DBNull.Value : location.Trim());
            command.Parameters.AddWithValue("@interviewer_employee_id", interviewerEmployeeId.HasValue && interviewerEmployeeId.Value > 0 ? interviewerEmployeeId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@status", NormalizeInterviewStatus(status));
            command.Parameters.AddWithValue("@remarks", string.IsNullOrWhiteSpace(remarks) ? DBNull.Value : remarks.Trim());
            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteInterviewAsync(long interviewScheduleId)
        {
            const string sql = "DELETE FROM interview_schedules WHERE interview_schedule_id = @interview_schedule_id;";
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@interview_schedule_id", interviewScheduleId);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<IReadOnlyList<RecruitmentOfferDto>> GetOffersAsync(string? search = null, string? statusFilter = null, int limit = 500)
        {
            const string sql = @"
SELECT
    o.job_offer_id,
    o.job_application_id,
    CONCAT(a.last_name, ', ', a.first_name, IFNULL(CONCAT(' ', a.middle_name), '')) AS applicant_name,
    COALESCE(jp.posting_code, '-') AS posting_code,
    COALESCE(jp.title, '-') AS posting_title,
    o.offered_at,
    COALESCE(o.offer_status, 'PENDING') AS offer_status,
    o.salary_offer,
    o.start_date,
    COALESCE(o.remarks, '') AS remarks
FROM job_offers o
JOIN job_applications ja ON ja.job_application_id = o.job_application_id
JOIN applicants a ON a.applicant_id = ja.applicant_id
JOIN job_postings jp ON jp.job_posting_id = ja.job_posting_id
WHERE (@status IS NULL OR o.offer_status = @status)
  AND (@search = '' OR CONCAT(a.last_name, ', ', a.first_name, IFNULL(CONCAT(' ', a.middle_name), '')) LIKE CONCAT('%', @search, '%') OR COALESCE(jp.posting_code, '') LIKE CONCAT('%', @search, '%'))
ORDER BY o.offered_at DESC, o.job_offer_id DESC
LIMIT @limit;";

            var rows = new List<RecruitmentOfferDto>();
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@status", NormalizeFilter(statusFilter));
                command.Parameters.AddWithValue("@search", string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim());
                command.Parameters.AddWithValue("@limit", Math.Max(1, limit));
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rows.Add(new RecruitmentOfferDto(
                        ToLong(reader["job_offer_id"]),
                        ToLong(reader["job_application_id"]),
                        reader["applicant_name"]?.ToString() ?? "-",
                        reader["posting_code"]?.ToString() ?? "-",
                        reader["posting_title"]?.ToString() ?? "-",
                        reader["offered_at"] == DBNull.Value ? DateTime.Today : ToDateTime(reader["offered_at"]),
                        reader["offer_status"]?.ToString() ?? "PENDING",
                        reader["salary_offer"] == DBNull.Value ? null : ToDecimal(reader["salary_offer"]),
                        reader["start_date"] == DBNull.Value ? null : ToDate(reader["start_date"]),
                        reader["remarks"]?.ToString() ?? string.Empty));
                }
            }
            catch (MySqlException ex) when (IsMissingObjectError(ex))
            {
                return Array.Empty<RecruitmentOfferDto>();
            }

            return rows;
        }

        public async Task<long> AddOfferAsync(long jobApplicationId, decimal? salaryOffer, DateTime? startDate, string? offerStatus, string? remarks)
        {
            const string sql = @"INSERT INTO job_offers (job_application_id, offer_status, salary_offer, start_date, remarks) VALUES (@job_application_id, @offer_status, @salary_offer, @start_date, @remarks); SELECT LAST_INSERT_ID();";
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@job_application_id", jobApplicationId);
            command.Parameters.AddWithValue("@offer_status", NormalizeOfferStatus(offerStatus));
            command.Parameters.AddWithValue("@salary_offer", salaryOffer.HasValue ? salaryOffer.Value : DBNull.Value);
            command.Parameters.AddWithValue("@start_date", startDate.HasValue ? startDate.Value.Date : DBNull.Value);
            command.Parameters.AddWithValue("@remarks", string.IsNullOrWhiteSpace(remarks) ? DBNull.Value : remarks.Trim());
            var result = await command.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }

        public async Task UpdateOfferAsync(long jobOfferId, string? offerStatus, decimal? salaryOffer, DateTime? startDate, string? remarks)
        {
            const string sql = @"UPDATE job_offers SET offer_status = @offer_status, salary_offer = @salary_offer, start_date = @start_date, remarks = @remarks WHERE job_offer_id = @job_offer_id;";
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@job_offer_id", jobOfferId);
            command.Parameters.AddWithValue("@offer_status", NormalizeOfferStatus(offerStatus));
            command.Parameters.AddWithValue("@salary_offer", salaryOffer.HasValue ? salaryOffer.Value : DBNull.Value);
            command.Parameters.AddWithValue("@start_date", startDate.HasValue ? startDate.Value.Date : DBNull.Value);
            command.Parameters.AddWithValue("@remarks", string.IsNullOrWhiteSpace(remarks) ? DBNull.Value : remarks.Trim());
            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteOfferAsync(long jobOfferId)
        {
            const string sql = "DELETE FROM job_offers WHERE job_offer_id = @job_offer_id;";
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@job_offer_id", jobOfferId);
            await command.ExecuteNonQueryAsync();
        }

        private static string? NormalizeFilter(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Trim().ToUpperInvariant();
            return normalized == "ALL" ? null : normalized;
        }

        private static string NormalizeEmploymentType(string? value)
        {
            return (value?.Trim().ToUpperInvariant()) switch
            {
                "PLANTILLA" => "PLANTILLA",
                "CASUAL" => "CASUAL",
                "JOB_ORDER" => "JOB_ORDER",
                "CONTRACTUAL" => "CONTRACTUAL",
                "TEMPORARY" => "TEMPORARY",
                _ => "CASUAL"
            };
        }

        private static string NormalizePostingStatus(string? value)
        {
            return (value?.Trim().ToUpperInvariant()) switch
            {
                "DRAFT" => "DRAFT",
                "OPEN" => "OPEN",
                "CLOSED" => "CLOSED",
                "CANCELLED" => "CANCELLED",
                _ => "OPEN"
            };
        }

        private static string NormalizeApplicationStatus(string? value)
        {
            return (value?.Trim().ToUpperInvariant()) switch
            {
                "SUBMITTED" => "SUBMITTED",
                "SCREENING" => "SCREENING",
                "SHORTLISTED" => "SHORTLISTED",
                "INTERVIEW" => "INTERVIEW",
                "OFFERED" => "OFFERED",
                "HIRED" => "HIRED",
                "REJECTED" => "REJECTED",
                "WITHDRAWN" => "WITHDRAWN",
                _ => "SUBMITTED"
            };
        }

        private static string NormalizeInterviewType(string? value)
        {
            return (value?.Trim().ToUpperInvariant()) switch
            {
                "PHONE" => "PHONE",
                "ONLINE" => "ONLINE",
                _ => "ONSITE"
            };
        }

        private static string NormalizeInterviewStatus(string? value)
        {
            return (value?.Trim().ToUpperInvariant()) switch
            {
                "DONE" => "DONE",
                "CANCELLED" => "CANCELLED",
                "NO_SHOW" => "NO_SHOW",
                _ => "SCHEDULED"
            };
        }

        private static string NormalizeOfferStatus(string? value)
        {
            return (value?.Trim().ToUpperInvariant()) switch
            {
                "ACCEPTED" => "ACCEPTED",
                "DECLINED" => "DECLINED",
                "CANCELLED" => "CANCELLED",
                _ => "PENDING"
            };
        }

        private static bool IsMissingObjectError(MySqlException ex) => ex.Number == 1049 || ex.Number == 1146;
        private static int ToInt(object value) => value == DBNull.Value ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        private static long ToLong(object value) => value == DBNull.Value ? 0L : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        private static decimal ToDecimal(object value) => value == DBNull.Value ? 0m : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        private static DateTime ToDate(object value) => Convert.ToDateTime(value, CultureInfo.InvariantCulture).Date;
        private static DateTime ToDateTime(object value) => Convert.ToDateTime(value, CultureInfo.InvariantCulture);
    }
}
