using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HRMS.Model
{
    public record TrainingCourseDto(
        int Id,
        string Title,
        string Provider,
        string Description,
        double Hours,
        string Status);

    public record TrainingEnrollmentDto(
        string Employee,
        string Course,
        DateTime? ScheduleDate,
        string Status);

    public record TrainingEmployeeDto(int Id, string Name);

    public class TrainingDataService
    {
        private static int _nextCourseId;
        private static int _nextEnrollmentId;

        public TrainingDataService(string connectionString)
        {
            ArgumentNullException.ThrowIfNull(connectionString);
        }

        public Task<IReadOnlyList<TrainingCourseDto>> GetCoursesAsync()
        {
            IReadOnlyList<TrainingCourseDto> list = Array.Empty<TrainingCourseDto>();
            return Task.FromResult(list);
        }

        public Task UpdateCourseAsync(TrainingCourseDto course)
        {
            return Task.CompletedTask;
        }

        public Task<int> AddCourseAsync(TrainingCourseDto course)
        {
            var id = Interlocked.Increment(ref _nextCourseId);
            return Task.FromResult(id);
        }

        public Task DeleteCourseAsync(int courseId)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TrainingEmployeeDto>> GetEmployeesAsync()
        {
            IReadOnlyList<TrainingEmployeeDto> list = Array.Empty<TrainingEmployeeDto>();
            return Task.FromResult(list);
        }

        public Task<int> AddEnrollmentAsync(int employeeId, int courseId, DateTime scheduleDate, string status)
        {
            var id = Interlocked.Increment(ref _nextEnrollmentId);
            return Task.FromResult(id);
        }

        public Task<IReadOnlyList<TrainingEnrollmentDto>> GetEnrollmentsAsync()
        {
            IReadOnlyList<TrainingEnrollmentDto> list = Array.Empty<TrainingEnrollmentDto>();
            return Task.FromResult(list);
        }
    }
}
