using CampusCare.Core.DTOs;
using CampusCare.Core.Entities;
using CampusCare.Core.Enums;
using CampusCare.Core.Interfaces;
using CampusCare.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CampusCare.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ComplaintsApiController : ControllerBase
    {
        private readonly IComplaintRepository _complaintRepository;
        private readonly IAIService _aiService;
        private readonly INotificationService _notificationService;
        private readonly IEscalationService _escalationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ComplaintsApiController(
            IComplaintRepository complaintRepository,
            IAIService aiService,
            INotificationService notificationService,
            IEscalationService escalationService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _complaintRepository = complaintRepository;
            _aiService = aiService;
            _notificationService = notificationService;
            _escalationService = escalationService;
            _userManager = userManager;
            _context = context;
        }

        //Retrieves a list of complaints matching the query filter.
    
        [HttpGet]
        public async Task<IActionResult> GetComplaints([FromQuery] ComplaintQueryFilter filter)
        {
            IEnumerable<Complaint> complaints;

            if (!string.IsNullOrEmpty(filter.StudentId))
            {
                complaints = await _complaintRepository.GetByStudentIdAsync(filter.StudentId);
            }
            else if (!string.IsNullOrEmpty(filter.StaffId))
            {
                complaints = await _complaintRepository.GetByAssignedStaffIdAsync(filter.StaffId);
            }
            else if (filter.DepartmentId.HasValue && filter.DepartmentId.Value > 0)
            {
                complaints = await _complaintRepository.GetByDepartmentIdAsync(filter.DepartmentId.Value);
            }
            else
            {
                complaints = await _complaintRepository.GetAllAsync();
            }

            var result = complaints.AsEnumerable();

            if (!string.IsNullOrEmpty(filter.Status) && Enum.TryParse<ComplaintStatus>(filter.Status, out var statusEnum))
            {
                result = result.Where(c => c.Status == statusEnum);
            }

            if (filter.CategoryId.HasValue && filter.CategoryId.Value > 0)
            {
                result = result.Where(c => c.CategoryId == filter.CategoryId.Value);
            }

            if (filter.UnassignedOnly == true)
            {
                result = result.Where(c => string.IsNullOrEmpty(c.AssignedStaffId));
            }

            if (filter.EscalatedOnly == true)
            {
                result = result.Where(c => c.IsEscalated || c.Status == ComplaintStatus.Escalated);
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                string s = filter.Search.Trim().ToLower();
                result = result.Where(c => (c.ComplaintNumber != null && c.ComplaintNumber.ToLower().Contains(s))
                                        || (c.Title != null && c.Title.ToLower().Contains(s))
                                        || (c.Location != null && c.Location.ToLower().Contains(s)));
            }

            return Ok(result.ToList());
        }

        /// Retrieves detailed complaint information by ID including AI triage, comments, history, and feedback.
        [HttpGet("{id}")]
        public async Task<IActionResult> GetComplaintDetails(int id)
        {
            var complaint = await _complaintRepository.GetByIdAsync(id);
            if (complaint == null) return NotFound(new { Message = $"Complaint ID {id} not found." });

            return Ok(complaint);
        }


        /// Creates a new complaint, applies AI triage, generates tracking ID, creates history, and fires notifications.
        [HttpPost]
        public async Task<IActionResult> CreateComplaint([FromBody] CreateComplaintDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Title) || string.IsNullOrWhiteSpace(model.Description))
            {
                return BadRequest(new { Message = "Title and Description are required." });
            }

            // 1. AI Analysis
            var aiResult = await _aiService.AnalyzeComplaintAsync(model.Title, model.Description, model.Location);

            // 2. Resolve Category & Department
            int categoryId;
            int departmentId;

            var adminDept = await _context.Departments.FirstOrDefaultAsync(d => d.Code == "ADMIN")
                            ?? await _context.Departments.FirstOrDefaultAsync();
            int fallbackDeptId = adminDept?.Id ?? 1;

            if (model.CategoryId.HasValue && model.CategoryId.Value > 0)
            {
                categoryId = model.CategoryId.Value;
                var cat = await _context.ComplaintCategories.FindAsync(categoryId);
                departmentId = cat?.DefaultDepartmentId ?? fallbackDeptId;
            }
            else
            {
                var matchedCategory = await _context.ComplaintCategories
                    .FirstOrDefaultAsync(c => c.Name.ToLower() == aiResult.Category.ToLower() || c.Name.ToLower().Contains(aiResult.Category.ToLower()) || aiResult.Category.ToLower().Contains(c.Name.ToLower()));

                var matchedDepartment = await _context.Departments
                    .FirstOrDefaultAsync(d => d.Name.ToLower() == aiResult.Department.ToLower()
                                            || d.Name.ToLower().Contains(aiResult.Department.ToLower())
                                            || aiResult.Department.ToLower().Contains(d.Name.ToLower())
                                            || d.Code.ToLower() == aiResult.Department.ToLower());

                if (matchedCategory != null)
                {
                    categoryId = matchedCategory.Id;
                    departmentId = matchedDepartment?.Id ?? matchedCategory.DefaultDepartmentId;
                }
                else if (matchedDepartment != null)
                {
                    departmentId = matchedDepartment.Id;
                    var catInDept = await _context.ComplaintCategories.FirstOrDefaultAsync(c => c.DefaultDepartmentId == matchedDepartment.Id)
                                    ?? await _context.ComplaintCategories.FirstOrDefaultAsync(c => c.Name == "Other")
                                    ?? await _context.ComplaintCategories.FirstOrDefaultAsync();
                    categoryId = catInDept?.Id ?? 1;
                }
                else
                {
                    var otherCat = await _context.ComplaintCategories.FirstOrDefaultAsync(c => c.Name == "Other")
                                   ?? await _context.ComplaintCategories.FirstOrDefaultAsync();
                    categoryId = otherCat?.Id ?? 1;
                    departmentId = otherCat?.DefaultDepartmentId ?? fallbackDeptId;
                }
            }

            // 3. Generate Sequence Number
            string complaintNumber = await _complaintRepository.GenerateUniqueComplaintNumberAsync();

            var complaint = new Complaint
            {
                ComplaintNumber = complaintNumber,
                Title = model.Title,
                Description = model.Description,
                Location = model.Location,
                Status = ComplaintStatus.Submitted,
                Priority = model.Priority != PriorityLevel.Medium ? model.Priority : aiResult.Priority,
                CategoryId = categoryId,
                DepartmentId = departmentId,
                StudentId = model.StudentId,
                CreatedAt = DateTime.UtcNow
            };

            // 4. Attachments
            if (!string.IsNullOrEmpty(model.AttachmentFileName) && !string.IsNullOrEmpty(model.AttachmentFilePath))
            {
                complaint.Attachments.Add(new ComplaintAttachment
                {
                    FileName = model.AttachmentFileName,
                    FilePath = model.AttachmentFilePath,
                    ContentType = model.AttachmentContentType ?? "application/octet-stream",
                    FileSize = model.AttachmentFileSize ?? 0,
                    UploadedAt = DateTime.UtcNow
                });
            }

            // 5. AI Analysis Record
            complaint.AIAnalysis = new AIAnalysis
            {
                SuggestedCategory = aiResult.Category,
                SuggestedPriority = aiResult.Priority,
                SuggestedDepartment = aiResult.Department,
                GeneratedSummary = aiResult.Summary,
                ModelUsed = aiResult.ModelUsed,
                ConfidenceScore = aiResult.IsSuccess ? 0.88 : 0.50,
                AnalyzedAt = DateTime.UtcNow
            };

            // 6. Initial History Entry
            complaint.History.Add(new ComplaintHistory
            {
                ChangedByUserId = model.StudentId,
                Action = "Submitted",
                OldStatus = null,
                NewStatus = ComplaintStatus.Submitted,
                Timestamp = DateTime.UtcNow,
                Notes = "Complaint submitted by student via Web API."
            });

            await _complaintRepository.AddAsync(complaint);

            // 7. Trigger Notifications
            var deptObj = await _context.Departments.FindAsync(departmentId);
            await _notificationService.SendInAppNotificationAsync(
                model.StudentId,
                "Complaint Created",
                $"Your complaint {complaintNumber} has been submitted successfully.",
                complaint.Id
            );

            await _notificationService.SendNotificationAsync(new NotificationPayload
            {
                EventType = "NewComplaint",
                ComplaintId = complaint.Id,
                ComplaintNumber = complaint.ComplaintNumber,
                Title = complaint.Title,
                Status = complaint.Status.ToString(),
                Priority = complaint.Priority.ToString(),
                Department = deptObj?.Name ?? "General",
                StudentEmail = model.StudentEmail ?? string.Empty,
                Timestamp = DateTime.UtcNow
            });

            var createdComplaint = await _complaintRepository.GetByIdAsync(complaint.Id);
            return CreatedAtAction(nameof(GetComplaintDetails), new { id = complaint.Id }, createdComplaint);
        }

        /// Updates the status of a complaint following state machine rules.
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateComplaintStatus(int id, [FromBody] UpdateComplaintStatusDto model)
        {
            var complaint = await _complaintRepository.GetByIdAsync(id);
            if (complaint == null) return NotFound(new { Message = $"Complaint ID {id} not found." });

            if (!IsValidStateTransition(complaint.Status, model.NewStatus))
            {
                return BadRequest(new { Message = $"Invalid status transition from '{complaint.Status}' to '{model.NewStatus}'." });
            }

            if (model.NewStatus == ComplaintStatus.Resolved && string.IsNullOrWhiteSpace(model.ResolutionDetails))
            {
                return BadRequest(new { Message = "Resolution details are required when marking a complaint as Resolved." });
            }

            var oldStatus = complaint.Status;
            complaint.Status = model.NewStatus;
            complaint.UpdatedAt = DateTime.UtcNow;

            if (model.NewStatus == ComplaintStatus.Resolved)
            {
                complaint.ResolvedAt = DateTime.UtcNow;
                complaint.ResolutionDetails = model.ResolutionDetails;
            }
            else if (model.NewStatus == ComplaintStatus.Closed)
            {
                complaint.ClosedAt = DateTime.UtcNow;
            }

            complaint.History.Add(new ComplaintHistory
            {
                ComplaintId = complaint.Id,
                ChangedByUserId = model.UserId,
                Action = $"Status updated to {model.NewStatus}",
                OldStatus = oldStatus,
                NewStatus = model.NewStatus,
                Timestamp = DateTime.UtcNow,
                Notes = model.CommentText ?? (model.NewStatus == ComplaintStatus.Resolved ? model.ResolutionDetails : null)
            });

            if (!string.IsNullOrWhiteSpace(model.CommentText))
            {
                complaint.Comments.Add(new ComplaintComment
                {
                    ComplaintId = complaint.Id,
                    UserId = model.UserId,
                    CommentText = model.CommentText.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    IsInternalOnly = model.IsInternalOnly
                });
            }

            await _complaintRepository.UpdateAsync(complaint);

            // Notifications
            await _notificationService.SendInAppNotificationAsync(
                complaint.StudentId,
                $"Complaint {complaint.ComplaintNumber} Updated",
                $"Status changed to '{model.NewStatus}'.",
                complaint.Id
            );

            if (model.NewStatus == ComplaintStatus.Resolved)
            {
                await _notificationService.SendNotificationAsync(new NotificationPayload
                {
                    EventType = "ComplaintResolved",
                    ComplaintId = complaint.Id,
                    ComplaintNumber = complaint.ComplaintNumber,
                    Title = complaint.Title,
                    Status = complaint.Status.ToString(),
                    Priority = complaint.Priority.ToString(),
                    Department = complaint.Department?.Name ?? "General",
                    StudentEmail = complaint.Student?.Email ?? string.Empty,
                    StaffEmail = model.UserEmail,
                    Timestamp = DateTime.UtcNow
                });
            }

            return Ok(new { Success = true, Message = $"Complaint status updated to {model.NewStatus}." });
        }

        /// Assigns a staff member and updates priority / category for a complaint.
        [HttpPut("{id}/assign")]
        public async Task<IActionResult> AssignStaff(int id, [FromBody] AssignComplaintDto model)
        {
            var complaint = await _complaintRepository.GetByIdAsync(id);
            if (complaint == null) return NotFound(new { Message = $"Complaint ID {id} not found." });

            var assignedStaff = await _userManager.FindByIdAsync(model.SelectedStaffId);
            if (assignedStaff == null)
            {
                return BadRequest(new { Message = "Selected staff member does not exist." });
            }

            var oldStaff = complaint.AssignedStaff?.FullName ?? "Unassigned";
            var oldStatus = complaint.Status;

            complaint.AssignedStaffId = model.SelectedStaffId;
            complaint.Priority = model.Priority;
            if (model.CategoryId > 0)
            {
                complaint.CategoryId = model.CategoryId;
            }
            complaint.UpdatedAt = DateTime.UtcNow;

            if (complaint.Status == ComplaintStatus.Submitted)
            {
                complaint.Status = ComplaintStatus.Assigned;
            }

            complaint.History.Add(new ComplaintHistory
            {
                ComplaintId = complaint.Id,
                ChangedByUserId = model.ManagerId,
                Action = $"Assigned to {assignedStaff.FullName}",
                OldStatus = oldStatus,
                NewStatus = complaint.Status,
                Timestamp = DateTime.UtcNow,
                Notes = model.Note ?? $"Reassigned from {oldStaff} to {assignedStaff.FullName}"
            });

            await _complaintRepository.UpdateAsync(complaint);

            // Notifications
            await _notificationService.SendInAppNotificationAsync(
                assignedStaff.Id,
                "New Complaint Assigned",
                $"You have been assigned complaint {complaint.ComplaintNumber}: {complaint.Title}.",
                complaint.Id
            );

            await _notificationService.SendNotificationAsync(new NotificationPayload
            {
                EventType = "ComplaintAssigned",
                ComplaintId = complaint.Id,
                ComplaintNumber = complaint.ComplaintNumber,
                Title = complaint.Title,
                Status = complaint.Status.ToString(),
                Priority = complaint.Priority.ToString(),
                Department = complaint.Department?.Name ?? "General",
                StudentEmail = complaint.Student?.Email ?? string.Empty,
                StaffEmail = assignedStaff.Email,
                ManagerEmail = model.ManagerEmail,
                Timestamp = DateTime.UtcNow
            });

            return Ok(new { Success = true, Message = $"Complaint {complaint.ComplaintNumber} assigned to {assignedStaff.FullName}." });
        }

        /// <summary>
        /// Adds a comment to a complaint.
        /// </summary>
        [HttpPost("{id}/comments")]
        public async Task<IActionResult> AddComment(int id, [FromBody] AddCommentDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.CommentText))
            {
                return BadRequest(new { Message = "Comment text cannot be empty." });
            }

            var complaint = await _complaintRepository.GetByIdAsync(id);
            if (complaint == null) return NotFound(new { Message = $"Complaint ID {id} not found." });

            var comment = new ComplaintComment
            {
                ComplaintId = id,
                UserId = model.UserId,
                CommentText = model.CommentText.Trim(),
                CreatedAt = DateTime.UtcNow,
                IsInternalOnly = model.IsInternalOnly
            };

            _context.ComplaintComments.Add(comment);
            await _context.SaveChangesAsync();

            return Ok(new { Success = true, Message = "Comment added successfully." });
        }


        /// Submits 1-5 star student feedback on a resolved/closed complaint.
        [HttpPost("{id}/feedback")]
        public async Task<IActionResult> SubmitFeedback(int id, [FromBody] SubmitFeedbackDto model)
        {
            var complaint = await _complaintRepository.GetByIdAsync(id);
            if (complaint == null) return NotFound(new { Message = $"Complaint ID {id} not found." });

            if (complaint.Status != ComplaintStatus.Resolved && complaint.Status != ComplaintStatus.Closed)
            {
                return BadRequest(new { Message = "Feedback can only be submitted after complaint resolution." });
            }

            var existingFeedback = await _context.Feedbacks.FirstOrDefaultAsync(f => f.ComplaintId == id);
            if (existingFeedback != null)
            {
                return BadRequest(new { Message = "Feedback has already been submitted for this complaint." });
            }

            var feedback = new Feedback
            {
                ComplaintId = id,
                StudentId = model.StudentId,
                Rating = model.Rating,
                Comment = model.Comment,
                SubmittedAt = DateTime.UtcNow
            };

            _context.Feedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            return Ok(new { Success = true, Message = "Feedback submitted successfully." });
        }

        /// Permanently deletes a complaint and all associated records.

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteComplaint(int id)
        {
            var complaint = await _complaintRepository.GetByIdAsync(id);
            if (complaint == null)
            {
                return NotFound(new { Success = false, Message = $"Complaint ID {id} not found." });
            }

            string number = complaint.ComplaintNumber;
            await _complaintRepository.DeleteAsync(id);
            return Ok(new
            {
                Success = true,
                Message = $"Complaint '{number}' (ID: {id}) permanently deleted.",
                Timestamp = DateTime.UtcNow
            });
        }


        // Purges historical complaints (Closed/Resolved/Rejected) older than specified days.

        [HttpPost("purge")]
        public async Task<IActionResult> PurgeComplaints([FromBody] PurgeComplaintsDto model)
        {
            int days = model?.DaysOlderThan ?? 30;
            var cutoff = DateTime.UtcNow.AddDays(-days);

            var complaintsToPurge = await _context.Complaints
                .Where(c => (c.Status == ComplaintStatus.Closed || c.Status == ComplaintStatus.Resolved || c.Status == ComplaintStatus.Rejected)
                            && c.CreatedAt <= cutoff)
                .ToListAsync();

            int count = complaintsToPurge.Count;
            foreach (var c in complaintsToPurge)
            {
                await _complaintRepository.DeleteAsync(c.Id);
            }

            return Ok(new
            {
                Success = true,
                Message = $"Purged {count} complaints older than {days} days.",
                PurgedCount = count,
                Timestamp = DateTime.UtcNow
            });
        }


        // Triggers SLA breach escalation check for overdue complaints (> 48h).

        [HttpPost("escalate-overdue")]
        public async Task<IActionResult> TriggerEscalationCheck([FromQuery] int overdueHours = 48)
        {
            int count = await _escalationService.ProcessOverdueComplaintsAsync(overdueHours);
            return Ok(new
            {
                Success = true,
                Message = $"Escalation scan completed. {count} overdue complaints escalated.",
                EscalatedCount = count,
                Timestamp = DateTime.UtcNow
            });
        }

     
        // Endpoint receiving webhook callback notifications from n8n automation workflows.
        [HttpPost("n8n/webhook-callback")]
        public IActionResult ReceiveN8nWebhookCallback([FromBody] object payload)
        {
            Console.WriteLine($"[n8n Webhook Received Callback] {payload}");
            return Ok(new { Status = "Acknowledged", Timestamp = DateTime.UtcNow });
        }

        private static bool IsValidStateTransition(ComplaintStatus current, ComplaintStatus target)
        {
            if (current == target) return true;

            return current switch
            {
                ComplaintStatus.Submitted => target == ComplaintStatus.Assigned || target == ComplaintStatus.InProgress || target == ComplaintStatus.Rejected,
                ComplaintStatus.Assigned => target == ComplaintStatus.InProgress || target == ComplaintStatus.Rejected || target == ComplaintStatus.Escalated,
                ComplaintStatus.InProgress => target == ComplaintStatus.Resolved || target == ComplaintStatus.Escalated || target == ComplaintStatus.Rejected,
                ComplaintStatus.Escalated => target == ComplaintStatus.InProgress || target == ComplaintStatus.Resolved,
                ComplaintStatus.Resolved => target == ComplaintStatus.Closed || target == ComplaintStatus.InProgress,
                ComplaintStatus.Closed => false,
                ComplaintStatus.Rejected => false,
                _ => false
            };
        }
    }
}
