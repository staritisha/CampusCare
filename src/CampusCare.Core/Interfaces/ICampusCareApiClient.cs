using CampusCare.Core.DTOs;
using CampusCare.Core.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CampusCare.Core.Interfaces
{
    public interface ICampusCareApiClient
    {
        // Complaints
        Task<IEnumerable<Complaint>> GetComplaintsAsync(ComplaintQueryFilter? filter = null);
        Task<Complaint?> GetComplaintByIdAsync(int id);
        Task<Complaint?> CreateComplaintAsync(CreateComplaintDto dto);
        Task<bool> UpdateStatusAsync(UpdateComplaintStatusDto dto);
        Task<bool> AssignStaffAsync(AssignComplaintDto dto);
        Task<bool> DeleteComplaintAsync(int id);
        Task<int> PurgeComplaintsAsync(int daysOlderThan);
        Task<bool> AddCommentAsync(AddCommentDto dto);
        Task<bool> SubmitFeedbackAsync(SubmitFeedbackDto dto);
        Task<int> RunEscalationCheckAsync(int overdueHours = 48);

        // Departments
        Task<IEnumerable<Department>> GetDepartmentsAsync();
        Task<Department?> GetDepartmentByIdAsync(int id);
        Task<Department?> CreateDepartmentAsync(CreateDepartmentDto dto);
        Task<bool> UpdateDepartmentAsync(UpdateDepartmentDto dto);
        Task<bool> DeleteDepartmentAsync(int id);
        Task<IEnumerable<StaffListItemDto>> GetStaffByDepartmentAsync(int departmentId);

        // Categories
        Task<IEnumerable<ComplaintCategory>> GetCategoriesAsync();
        Task<ComplaintCategory?> GetCategoryByIdAsync(int id);
        Task<ComplaintCategory?> CreateCategoryAsync(CreateCategoryDto dto);
        Task<bool> UpdateCategoryAsync(UpdateCategoryDto dto);
        Task<bool> DeleteCategoryAsync(int id);

        // Users
        Task<IEnumerable<UserSummaryDto>> GetUsersAsync();
        Task<(bool Success, string? Error)> CreateStaffAsync(CreateStaffUserDto dto);
        Task<bool> ToggleUserStatusAsync(string userId);

        // Analytics
        Task<AdminAnalyticsDto> GetAdminAnalyticsAsync();
        Task<ManagerAnalyticsDto> GetManagerAnalyticsAsync(int departmentId);
        Task<StaffAnalyticsDto> GetStaffAnalyticsAsync(string staffId);
        Task<StudentAnalyticsDto> GetStudentAnalyticsAsync(string studentId);
    }
}
