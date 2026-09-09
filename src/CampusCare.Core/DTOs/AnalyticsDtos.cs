using System;
using System.Collections.Generic;

namespace CampusCare.Core.DTOs
{
    public class AdminAnalyticsDto
    {
        public int TotalComplaints { get; set; }
        public int PendingComplaints { get; set; }
        public int InProgressComplaints { get; set; }
        public int EscalatedComplaints { get; set; }
        public int ResolvedComplaints { get; set; }
        public double AverageResolutionHours { get; set; }
        public double AverageFeedbackRating { get; set; }

        public List<DepartmentStatDto> DepartmentStats { get; set; } = new();
        public List<CategoryStatDto> CategoryStats { get; set; } = new();
    }

    public class DepartmentStatDto
    {
        public string DepartmentName { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public int ResolvedCount { get; set; }
        public int EscalatedCount { get; set; }
    }

    public class CategoryStatDto
    {
        public string CategoryName { get; set; } = string.Empty;
        public int TotalCount { get; set; }
    }

    public class ManagerAnalyticsDto
    {
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int TotalDepartmentComplaints { get; set; }
        public int UnassignedCount { get; set; }
        public int InProgressCount { get; set; }
        public int EscalatedCount { get; set; }
        public int ResolvedCount { get; set; }
        public List<StaffWorkloadDto> StaffWorkload { get; set; } = new();
    }

    public class StaffWorkloadDto
    {
        public string StaffId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int ActiveAssignedCount { get; set; }
        public int ResolvedCount { get; set; }
    }

    public class StaffAnalyticsDto
    {
        public string StaffId { get; set; } = string.Empty;
        public int TotalAssigned { get; set; }
        public int PendingAction { get; set; }
        public int InProgress { get; set; }
        public int Escalated { get; set; }
        public int Resolved { get; set; }
    }

    public class StudentAnalyticsDto
    {
        public string StudentId { get; set; } = string.Empty;
        public int TotalComplaints { get; set; }
        public int PendingComplaints { get; set; }
        public int InProgressComplaints { get; set; }
        public int ResolvedComplaints { get; set; }
    }
}
