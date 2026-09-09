using CampusCare.Core.DTOs;
using CampusCare.Core.Entities;
using CampusCare.Core.Enums;
using CampusCare.Core.Interfaces;
using CampusCare.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CampusCare.Web.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly ICampusCareApiClient _apiClient;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public StudentController(
            ICampusCareApiClient apiClient,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment webHostEnvironment)
        {
            _apiClient = apiClient;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var stats = await _apiClient.GetStudentAnalyticsAsync(user.Id);
            var complaints = await _apiClient.GetComplaintsAsync(new ComplaintQueryFilter { StudentId = user.Id });
            var complaintList = complaints.ToList();

            var viewModel = new StudentDashboardViewModel
            {
                TotalComplaints = stats.TotalComplaints,
                PendingComplaints = stats.PendingComplaints,
                InProgressComplaints = stats.InProgressComplaints,
                ResolvedComplaints = stats.ResolvedComplaints,
                Complaints = complaintList
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await PopulateCategoriesViewBagAsync();
            return View(new CreateComplaintViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateComplaintViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (!ModelState.IsValid)
            {
                await PopulateCategoriesViewBagAsync();
                return View(model);
            }

            string? attachmentFileName = null;
            string? attachmentFilePath = null;
            string? attachmentContentType = null;
            long? attachmentFileSize = null;

            // Handle Optional File Attachment locally
            if (model.Attachment != null && model.Attachment.Length > 0)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.Attachment.FileName)}";
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.Attachment.CopyToAsync(fileStream);
                }

                attachmentFileName = model.Attachment.FileName;
                attachmentFilePath = $"/uploads/{uniqueFileName}";
                attachmentContentType = model.Attachment.ContentType;
                attachmentFileSize = model.Attachment.Length;
            }

            var dto = new CreateComplaintDto
            {
                Title = model.Title,
                Description = model.Description,
                Location = model.Location,
                CategoryId = model.CategoryId,
                Priority = model.Priority,
                StudentId = user.Id,
                StudentEmail = user.Email,
                AttachmentFileName = attachmentFileName,
                AttachmentFilePath = attachmentFilePath,
                AttachmentContentType = attachmentContentType,
                AttachmentFileSize = attachmentFileSize
            };

            var createdComplaint = await _apiClient.CreateComplaintAsync(dto);
            if (createdComplaint == null)
            {
                ModelState.AddModelError(string.Empty, "An error occurred while creating your complaint via Web API.");
                await PopulateCategoriesViewBagAsync();
                return View(model);
            }

            TempData["SuccessMessage"] = $"Complaint {createdComplaint.ComplaintNumber} has been successfully submitted!";
            return RedirectToAction(nameof(Details), new { id = createdComplaint.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var complaint = await _apiClient.GetComplaintByIdAsync(id);
            if (complaint == null) return NotFound();

            // Authorization Check: Student can only view their own complaint
            if (complaint.StudentId != user.Id && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var viewModel = new ComplaintDetailsViewModel
            {
                Complaint = complaint,
                FeedbackInput = new SubmitFeedbackViewModel { ComplaintId = complaint.Id }
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int id, string commentText)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (string.IsNullOrWhiteSpace(commentText))
            {
                TempData["ErrorMessage"] = "Comment text cannot be empty.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var complaint = await _apiClient.GetComplaintByIdAsync(id);
            if (complaint == null) return NotFound();
            if (complaint.StudentId != user.Id) return Forbid();

            var success = await _apiClient.AddCommentAsync(new AddCommentDto
            {
                ComplaintId = id,
                UserId = user.Id,
                CommentText = commentText.Trim(),
                IsInternalOnly = false
            });

            if (success)
            {
                TempData["SuccessMessage"] = "Comment added successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to add comment via Web API.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitFeedback(SubmitFeedbackViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var complaint = await _apiClient.GetComplaintByIdAsync(model.ComplaintId);
            if (complaint == null) return NotFound();
            if (complaint.StudentId != user.Id) return Forbid();

            if (complaint.Status != ComplaintStatus.Resolved && complaint.Status != ComplaintStatus.Closed)
            {
                TempData["ErrorMessage"] = "Feedback can only be submitted after complaint resolution.";
                return RedirectToAction(nameof(Details), new { id = model.ComplaintId });
            }

            if (complaint.Feedback != null)
            {
                TempData["ErrorMessage"] = "You have already submitted feedback for this complaint.";
                return RedirectToAction(nameof(Details), new { id = model.ComplaintId });
            }

            var success = await _apiClient.SubmitFeedbackAsync(new SubmitFeedbackDto
            {
                ComplaintId = model.ComplaintId,
                StudentId = user.Id,
                Rating = model.Rating,
                Comment = model.Comment
            });

            if (success)
            {
                TempData["SuccessMessage"] = "Thank you for your feedback!";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to submit feedback via Web API.";
            }

            return RedirectToAction(nameof(Details), new { id = model.ComplaintId });
        }

        private async Task PopulateCategoriesViewBagAsync()
        {
            var categories = await _apiClient.GetCategoriesAsync();
            ViewBag.Categories = new SelectList(categories.Select(c => new { c.Id, c.Name }), "Id", "Name");
        }
    }
}
