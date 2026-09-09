using CampusCare.Core.DTOs;
using CampusCare.Core.Entities;
using CampusCare.Core.Enums;
using CampusCare.Core.Interfaces;
using CampusCare.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CampusCare.Web.Controllers
{
    [Authorize(Roles = "Manager,Admin")]
    public class ManagerController : Controller
    {
        private readonly ICampusCareApiClient _apiClient;
        private readonly UserManager<ApplicationUser> _userManager;

        public ManagerController(
            ICampusCareApiClient apiClient,
            UserManager<ApplicationUser> userManager)
        {
            _apiClient = apiClient;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? filter = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            int departmentId = user.DepartmentId ?? 1;
            var analytics = await _apiClient.GetManagerAnalyticsAsync(departmentId);
            var deptComplaints = await _apiClient.GetComplaintsAsync(new ComplaintQueryFilter { DepartmentId = departmentId });
            var complaintList = deptComplaints.ToList();

            // Filter logic for table display
            var displayComplaints = complaintList;
            if (filter == "unassigned") displayComplaints = complaintList.Where(c => string.IsNullOrEmpty(c.AssignedStaffId)).ToList();
            else if (filter == "escalated") displayComplaints = complaintList.Where(c => c.IsEscalated || c.Status == ComplaintStatus.Escalated).ToList();
            else if (filter == "inprogress") displayComplaints = complaintList.Where(c => c.Status == ComplaintStatus.InProgress).ToList();
            else if (filter == "resolved") displayComplaints = complaintList.Where(c => c.Status == ComplaintStatus.Resolved || c.Status == ComplaintStatus.Closed).ToList();

            var staffWorkloadList = analytics.StaffWorkload.Select(s => new StaffWorkloadItem
            {
                StaffId = s.StaffId,
                StaffName = s.StaffName,
                Email = s.Email,
                ActiveAssignedCount = s.ActiveAssignedCount,
                ResolvedCount = s.ResolvedCount
            }).ToList();

            var viewModel = new ManagerDashboardViewModel
            {
                DepartmentName = analytics.DepartmentName,
                TotalDepartmentComplaints = analytics.TotalDepartmentComplaints,
                UnassignedCount = analytics.UnassignedCount,
                InProgressCount = analytics.InProgressCount,
                EscalatedCount = analytics.EscalatedCount,
                ResolvedCount = analytics.ResolvedCount,
                Complaints = displayComplaints,
                StaffWorkload = staffWorkloadList
            };

            ViewBag.CurrentFilter = filter;
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Assign(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var complaint = await _apiClient.GetComplaintByIdAsync(id);
            if (complaint == null) return NotFound();

            int departmentId = user.DepartmentId ?? complaint.DepartmentId;

            var staffList = await _apiClient.GetStaffByDepartmentAsync(departmentId);
            ViewBag.StaffList = new SelectList(staffList.Select(s => new { s.Id, Name = $"{s.FullName} ({s.Email})" }), "Id", "Name", complaint.AssignedStaffId);

            var categories = await _apiClient.GetCategoriesAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", complaint.CategoryId);

            var viewModel = new AssignStaffViewModel
            {
                ComplaintId = complaint.Id,
                ComplaintNumber = complaint.ComplaintNumber,
                Title = complaint.Title,
                SelectedStaffId = complaint.AssignedStaffId ?? string.Empty,
                Priority = complaint.Priority,
                CategoryId = complaint.CategoryId
            };

            ViewBag.Complaint = complaint;
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(AssignStaffViewModel model)
        {
            var manager = await _userManager.GetUserAsync(User);
            if (manager == null) return Challenge();

            var success = await _apiClient.AssignStaffAsync(new AssignComplaintDto
            {
                ComplaintId = model.ComplaintId,
                ManagerId = manager.Id,
                ManagerEmail = manager.Email,
                SelectedStaffId = model.SelectedStaffId,
                Priority = model.Priority,
                CategoryId = model.CategoryId,
                Note = model.Note
            });

            if (success)
            {
                TempData["SuccessMessage"] = $"Complaint assigned successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to assign staff member via Web API.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
