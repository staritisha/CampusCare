using CampusCare.Core.DTOs;
using CampusCare.Core.Entities;
using CampusCare.Core.Enums;
using CampusCare.Core.Interfaces;
using CampusCare.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CampusCare.Web.Controllers
{
    [Authorize(Roles = "Staff,Manager,Admin")]
    public class StaffController : Controller
    {
        private readonly ICampusCareApiClient _apiClient;
        private readonly UserManager<ApplicationUser> _userManager;

        public StaffController(
            ICampusCareApiClient apiClient,
            UserManager<ApplicationUser> userManager)
        {
            _apiClient = apiClient;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? statusFilter = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var stats = await _apiClient.GetStaffAnalyticsAsync(user.Id);
            var complaints = await _apiClient.GetComplaintsAsync(new ComplaintQueryFilter
            {
                StaffId = user.Id,
                Status = statusFilter
            });
            var complaintList = complaints.ToList();

            var viewModel = new StaffDashboardViewModel
            {
                TotalAssigned = stats.TotalAssigned,
                PendingAction = stats.PendingAction,
                InProgress = stats.InProgress,
                Escalated = stats.Escalated,
                Resolved = stats.Resolved,
                Complaints = complaintList
            };

            ViewBag.StatusFilter = statusFilter;
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var complaint = await _apiClient.GetComplaintByIdAsync(id);
            if (complaint == null) return NotFound();

            var viewModel = new UpdateStatusViewModel
            {
                ComplaintId = complaint.Id,
                NewStatus = complaint.Status
            };

            ViewBag.Complaint = complaint;
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(UpdateStatusViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var complaint = await _apiClient.GetComplaintByIdAsync(model.ComplaintId);
            if (complaint == null) return NotFound();

            // Validate Workflow State Machine Rules
            if (!IsValidStateTransition(complaint.Status, model.NewStatus))
            {
                TempData["ErrorMessage"] = $"Invalid status transition from '{complaint.Status}' to '{model.NewStatus}'.";
                return RedirectToAction(nameof(Details), new { id = model.ComplaintId });
            }

            if (model.NewStatus == ComplaintStatus.Resolved && string.IsNullOrWhiteSpace(model.ResolutionDetails))
            {
                TempData["ErrorMessage"] = "Resolution details are required when marking a complaint as Resolved.";
                return RedirectToAction(nameof(Details), new { id = model.ComplaintId });
            }

            var success = await _apiClient.UpdateStatusAsync(new UpdateComplaintStatusDto
            {
                ComplaintId = model.ComplaintId,
                NewStatus = model.NewStatus,
                UserId = user.Id,
                UserEmail = user.Email,
                ResolutionDetails = model.ResolutionDetails,
                CommentText = model.CommentText,
                IsInternalOnly = model.IsInternalOnly
            });

            if (success)
            {
                TempData["SuccessMessage"] = $"Complaint status updated to '{model.NewStatus}'.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to update complaint status via Web API.";
            }

            return RedirectToAction(nameof(Details), new { id = model.ComplaintId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int complaintId, string commentText, bool isInternalOnly)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (string.IsNullOrWhiteSpace(commentText))
            {
                TempData["ErrorMessage"] = "Comment text cannot be empty.";
                return RedirectToAction(nameof(Details), new { id = complaintId });
            }

            var success = await _apiClient.AddCommentAsync(new AddCommentDto
            {
                ComplaintId = complaintId,
                UserId = user.Id,
                CommentText = commentText.Trim(),
                IsInternalOnly = isInternalOnly
            });

            if (success)
            {
                TempData["SuccessMessage"] = "Comment added successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to add comment via Web API.";
            }

            return RedirectToAction(nameof(Details), new { id = complaintId });
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
