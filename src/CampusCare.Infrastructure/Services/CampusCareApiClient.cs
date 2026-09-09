using CampusCare.Core.DTOs;
using CampusCare.Core.Entities;
using CampusCare.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CampusCare.Infrastructure.Services
{
    public class CampusCareApiClient : ICampusCareApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CampusCareApiClient> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public CampusCareApiClient(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<CampusCareApiClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            string baseUrl = configuration["WebApiSettings:BaseUrl"] ?? "http://localhost:5174";
            if (_httpClient.BaseAddress == null)
            {
                _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            }

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };
        }

        #region Complaints Web APIs

        public async Task<IEnumerable<Complaint>> GetComplaintsAsync(ComplaintQueryFilter? filter = null)
        {
            try
            {
                var queryParams = new List<string>();
                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.Status)) queryParams.Add($"status={Uri.EscapeDataString(filter.Status)}");
                    if (filter.DepartmentId.HasValue) queryParams.Add($"departmentId={filter.DepartmentId.Value}");
                    if (filter.CategoryId.HasValue) queryParams.Add($"categoryId={filter.CategoryId.Value}");
                    if (!string.IsNullOrEmpty(filter.StudentId)) queryParams.Add($"studentId={Uri.EscapeDataString(filter.StudentId)}");
                    if (!string.IsNullOrEmpty(filter.StaffId)) queryParams.Add($"staffId={Uri.EscapeDataString(filter.StaffId)}");
                    if (!string.IsNullOrEmpty(filter.Search)) queryParams.Add($"search={Uri.EscapeDataString(filter.Search)}");
                    if (filter.UnassignedOnly == true) queryParams.Add("unassignedOnly=true");
                    if (filter.EscalatedOnly == true) queryParams.Add("escalatedOnly=true");
                }

                string url = "api/complaints" + (queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty);
                var result = await _httpClient.GetFromJsonAsync<List<Complaint>>(url, _jsonOptions);
                return result ?? new List<Complaint>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling GetComplaintsAsync");
                return new List<Complaint>();
            }
        }

        public async Task<Complaint?> GetComplaintByIdAsync(int id)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<Complaint>($"api/complaints/{id}", _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling GetComplaintByIdAsync for ID {Id}", id);
                return null;
            }
        }

        public async Task<Complaint?> CreateComplaintAsync(CreateComplaintDto dto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/complaints", dto, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<Complaint>(_jsonOptions);
                }
                _logger.LogWarning("CreateComplaint failed with status code {StatusCode}", response.StatusCode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling CreateComplaintAsync");
                return null;
            }
        }

        public async Task<bool> UpdateStatusAsync(UpdateComplaintStatusDto dto)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"api/complaints/{dto.ComplaintId}/status", dto, _jsonOptions);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling UpdateStatusAsync for ID {Id}", dto.ComplaintId);
                return false;
            }
        }

        public async Task<bool> AssignStaffAsync(AssignComplaintDto dto)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"api/complaints/{dto.ComplaintId}/assign", dto, _jsonOptions);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling AssignStaffAsync for ID {Id}", dto.ComplaintId);
                return false;
            }
        }

        public async Task<bool> DeleteComplaintAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/complaints/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling DeleteComplaintAsync for ID {Id}", id);
                return false;
            }
        }

        public async Task<int> PurgeComplaintsAsync(int daysOlderThan)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/complaints/purge", new PurgeComplaintsDto { DaysOlderThan = daysOlderThan }, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
                    if (result.TryGetProperty("purgedCount", out var countElement))
                    {
                        return countElement.GetInt32();
                    }
                }
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling PurgeComplaintsAsync");
                return 0;
            }
        }

        public async Task<bool> AddCommentAsync(AddCommentDto dto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"api/complaints/{dto.ComplaintId}/comments", dto, _jsonOptions);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling AddCommentAsync for ID {Id}", dto.ComplaintId);
                return false;
            }
        }

        public async Task<bool> SubmitFeedbackAsync(SubmitFeedbackDto dto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"api/complaints/{dto.ComplaintId}/feedback", dto, _jsonOptions);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling SubmitFeedbackAsync for ID {Id}", dto.ComplaintId);
                return false;
            }
        }

        public async Task<int> RunEscalationCheckAsync(int overdueHours = 48)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/complaints/escalate-overdue?overdueHours={overdueHours}", null);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
                    if (result.TryGetProperty("escalatedCount", out var countElement))
                    {
                        return countElement.GetInt32();
                    }
                }
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling RunEscalationCheckAsync");
                return 0;
            }
        }

        #endregion

        #region Departments Web APIs

        public async Task<IEnumerable<Department>> GetDepartmentsAsync()
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<List<Department>>("api/departments", _jsonOptions);
                return result ?? new List<Department>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling GetDepartmentsAsync");
                return new List<Department>();
            }
        }

        public async Task<Department?> GetDepartmentByIdAsync(int id)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<Department>($"api/departments/{id}", _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling GetDepartmentByIdAsync for ID {Id}", id);
                return null;
            }
        }

        public async Task<Department?> CreateDepartmentAsync(CreateDepartmentDto dto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/departments", dto, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<Department>(_jsonOptions);
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling CreateDepartmentAsync");
                return null;
            }
        }

        public async Task<bool> UpdateDepartmentAsync(UpdateDepartmentDto dto)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"api/departments/{dto.Id}", dto, _jsonOptions);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling UpdateDepartmentAsync for ID {Id}", dto.Id);
                return false;
            }
        }

        public async Task<bool> DeleteDepartmentAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/departments/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling DeleteDepartmentAsync for ID {Id}", id);
                return false;
            }
        }

        public async Task<IEnumerable<StaffListItemDto>> GetStaffByDepartmentAsync(int departmentId)
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<List<StaffListItemDto>>($"api/departments/{departmentId}/staff", _jsonOptions);
                return result ?? new List<StaffListItemDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling GetStaffByDepartmentAsync for ID {Id}", departmentId);
                return new List<StaffListItemDto>();
            }
        }

        #endregion

        #region Categories Web APIs

        public async Task<IEnumerable<ComplaintCategory>> GetCategoriesAsync()
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<List<ComplaintCategory>>("api/categories", _jsonOptions);
                return result ?? new List<ComplaintCategory>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling GetCategoriesAsync");
                return new List<ComplaintCategory>();
            }
        }

        public async Task<ComplaintCategory?> GetCategoryByIdAsync(int id)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<ComplaintCategory>($"api/categories/{id}", _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling GetCategoryByIdAsync for ID {Id}", id);
                return null;
            }
        }

        public async Task<ComplaintCategory?> CreateCategoryAsync(CreateCategoryDto dto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/categories", dto, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ComplaintCategory>(_jsonOptions);
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling CreateCategoryAsync");
                return null;
            }
        }

        public async Task<bool> UpdateCategoryAsync(UpdateCategoryDto dto)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"api/categories/{dto.Id}", dto, _jsonOptions);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling UpdateCategoryAsync for ID {Id}", dto.Id);
                return false;
            }
        }

        public async Task<bool> DeleteCategoryAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/categories/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling DeleteCategoryAsync for ID {Id}", id);
                return false;
            }
        }

        #endregion

        #region Users Web APIs

        public async Task<IEnumerable<UserSummaryDto>> GetUsersAsync()
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<List<UserSummaryDto>>("api/users", _jsonOptions);
                return result ?? new List<UserSummaryDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling GetUsersAsync");
                return new List<UserSummaryDto>();
            }
        }

        public async Task<(bool Success, string? Error)> CreateStaffAsync(CreateStaffUserDto dto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/users/staff", dto, _jsonOptions);
                if (response.IsSuccessStatusCode)
                {
                    return (true, null);
                }
                var errorResult = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
                string message = errorResult.TryGetProperty("message", out var m) ? m.GetString() ?? "Failed to create staff" : "Failed to create staff";
                return (false, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling CreateStaffAsync");
                return (false, ex.Message);
            }
        }

        public async Task<bool> ToggleUserStatusAsync(string userId)
        {
            try
            {
                var response = await _httpClient.PutAsync($"api/users/{userId}/toggle-status", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ToggleUserStatusAsync for ID {Id}", userId);
                return false;
            }
        }

        #endregion

        #region Analytics Web APIs

        public async Task<AdminAnalyticsDto> GetAdminAnalyticsAsync()
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<AdminAnalyticsDto>("api/analytics/admin", _jsonOptions);
                return result ?? new AdminAnalyticsDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling GetAdminAnalyticsAsync");
                return new AdminAnalyticsDto();
            }
        }

        public async Task<ManagerAnalyticsDto> GetManagerAnalyticsAsync(int departmentId)
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<ManagerAnalyticsDto>($"api/analytics/manager/{departmentId}", _jsonOptions);
                return result ?? new ManagerAnalyticsDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling GetManagerAnalyticsAsync for Department {DeptId}", departmentId);
                return new ManagerAnalyticsDto();
            }
        }

        public async Task<StaffAnalyticsDto> GetStaffAnalyticsAsync(string staffId)
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<StaffAnalyticsDto>($"api/analytics/staff/{staffId}", _jsonOptions);
                return result ?? new StaffAnalyticsDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling GetStaffAnalyticsAsync for Staff {StaffId}", staffId);
                return new StaffAnalyticsDto();
            }
        }

        public async Task<StudentAnalyticsDto> GetStudentAnalyticsAsync(string studentId)
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<StudentAnalyticsDto>($"api/analytics/student/{studentId}", _jsonOptions);
                return result ?? new StudentAnalyticsDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling GetStudentAnalyticsAsync for Student {StudentId}", studentId);
                return new StudentAnalyticsDto();
            }
        }

        #endregion
    }
}
