using CampusCare.Core.Entities;
using CampusCare.Core.Enums;
using System;
using System.Collections.Generic;

namespace CampusCare.Core.DTOs
{
    public class CreateComplaintDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;
        public string StudentId { get; set; } = string.Empty;
        public string? StudentEmail { get; set; }
        public string? AttachmentFileName { get; set; }
        public string? AttachmentFilePath { get; set; }
        public string? AttachmentContentType { get; set; }
        public long? AttachmentFileSize { get; set; }
    }

    public class UpdateComplaintStatusDto
    {
        public int ComplaintId { get; set; }
        public ComplaintStatus NewStatus { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string? UserEmail { get; set; }
        public string? ResolutionDetails { get; set; }
        public string? CommentText { get; set; }
        public bool IsInternalOnly { get; set; }
    }

    public class AssignComplaintDto
    {
        public int ComplaintId { get; set; }
        public string ManagerId { get; set; } = string.Empty;
        public string? ManagerEmail { get; set; }
        public string SelectedStaffId { get; set; } = string.Empty;
        public PriorityLevel Priority { get; set; }
        public int CategoryId { get; set; }
        public string? Note { get; set; }
    }

    public class AddCommentDto
    {
        public int ComplaintId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string CommentText { get; set; } = string.Empty;
        public bool IsInternalOnly { get; set; }
    }

    public class SubmitFeedbackDto
    {
        public int ComplaintId { get; set; }
        public string StudentId { get; set; } = string.Empty;
        public int Rating { get; set; } = 5;
        public string? Comment { get; set; }
    }

    public class PurgeComplaintsDto
    {
        public int DaysOlderThan { get; set; } = 30;
    }

    public class ComplaintQueryFilter
    {
        public string? Status { get; set; }
        public int? DepartmentId { get; set; }
        public int? CategoryId { get; set; }
        public string? StudentId { get; set; }
        public string? StaffId { get; set; }
        public string? Search { get; set; }
        public bool? UnassignedOnly { get; set; }
        public bool? EscalatedOnly { get; set; }
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; } = true;
        public string? Message { get; set; }
        public T? Data { get; set; }
    }
}
