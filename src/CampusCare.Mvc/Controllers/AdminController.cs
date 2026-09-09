using CampusCare.Core.DTOs;
using CampusCare.Core.Entities;
using CampusCare.Core.Enums;
using CampusCare.Core.Interfaces;
using CampusCare.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CampusCare.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ICampusCareApiClient _apiClient;

        public AdminController(ICampusCareApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? status, int? departmentId, int? categoryId, string? search)
        {
            var stats = await _apiClient.GetAdminAnalyticsAsync();
            var complaints = await _apiClient.GetComplaintsAsync(new ComplaintQueryFilter
            {
                Status = status,
                DepartmentId = departmentId,
                CategoryId = categoryId,
                Search = search
            });

            var departments = await _apiClient.GetDepartmentsAsync();
            var categories = await _apiClient.GetCategoriesAsync();

            ViewBag.Departments = new SelectList(departments, "Id", "Name", departmentId);
            ViewBag.Categories = new SelectList(categories, "Id", "Name", categoryId);
            ViewBag.SelectedStatus = status;
            ViewBag.Search = search;

            var deptStats = stats.DepartmentStats.Select(d => new DepartmentStatItem
            {
                DepartmentName = d.DepartmentName,
                TotalCount = d.TotalCount,
                ResolvedCount = d.ResolvedCount,
                EscalatedCount = d.EscalatedCount
            }).ToList();

            var catStats = stats.CategoryStats.Select(c => new CategoryStatItem
            {
                CategoryName = c.CategoryName,
                TotalCount = c.TotalCount
            }).ToList();

            var viewModel = new AdminDashboardViewModel
            {
                TotalComplaints = stats.TotalComplaints,
                PendingComplaints = stats.PendingComplaints,
                InProgressComplaints = stats.InProgressComplaints,
                EscalatedComplaints = stats.EscalatedComplaints,
                ResolvedComplaints = stats.ResolvedComplaints,
                AverageResolutionHours = stats.AverageResolutionHours,
                AverageFeedbackRating = stats.AverageFeedbackRating,
                DepartmentStats = deptStats,
                CategoryStats = catStats,
                RecentComplaints = complaints
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Users()
        {
            var users = await _apiClient.GetUsersAsync();
            var departments = await _apiClient.GetDepartmentsAsync();

            var userItems = users.Select(u => new UserManagementItem
            {
                UserId = u.UserId,
                FullName = u.FullName,
                Email = u.Email,
                Role = u.Role,
                DepartmentName = u.DepartmentName,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt
            }).ToList();

            ViewBag.Departments = new SelectList(departments, "Id", "Name");
            return View(userItems);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStaff(CreateStaffViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _apiClient.CreateStaffAsync(new CreateStaffUserDto
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    Password = model.Password,
                    DepartmentId = model.DepartmentId,
                    Role = model.Role
                });

                if (result.Success)
                {
                    TempData["SuccessMessage"] = $"{model.Role} member '{model.FullName}' added successfully via Web API.";
                }
                else
                {
                    TempData["ErrorMessage"] = result.Error ?? "Failed to create staff member.";
                }
            }
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(string id)
        {
            var success = await _apiClient.ToggleUserStatusAsync(id);
            if (success)
            {
                TempData["SuccessMessage"] = "Account status updated successfully via Web API.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to update account status.";
            }
            return RedirectToAction(nameof(Users));
        }

        [HttpGet]
        public async Task<IActionResult> Departments()
        {
            var departments = await _apiClient.GetDepartmentsAsync();
            return View(departments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDepartment(CreateDepartmentViewModel model)
        {
            if (ModelState.IsValid)
            {
                var created = await _apiClient.CreateDepartmentAsync(new CreateDepartmentDto
                {
                    Name = model.Name,
                    Code = model.Code.ToUpper(),
                    Description = model.Description
                });

                if (created != null)
                {
                    TempData["SuccessMessage"] = $"Department '{model.Name}' created successfully via Web API.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to create department via Web API.";
                }
            }
            return RedirectToAction(nameof(Departments));
        }

        [HttpGet]
        public async Task<IActionResult> Categories()
        {
            var categories = await _apiClient.GetCategoriesAsync();
            var departments = await _apiClient.GetDepartmentsAsync();

            ViewBag.Departments = new SelectList(departments, "Id", "Name");
            return View(categories);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(CreateCategoryViewModel model)
        {
            if (ModelState.IsValid)
            {
                var created = await _apiClient.CreateCategoryAsync(new CreateCategoryDto
                {
                    Name = model.Name,
                    Description = model.Description,
                    DefaultDepartmentId = model.DefaultDepartmentId
                });

                if (created != null)
                {
                    TempData["SuccessMessage"] = $"Category '{model.Name}' created successfully via Web API.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to create category via Web API.";
                }
            }
            return RedirectToAction(nameof(Categories));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunEscalationCheck()
        {
            int escalatedCount = await _apiClient.RunEscalationCheckAsync(48);
            TempData["SuccessMessage"] = $"Automated SLA escalation complete. {escalatedCount} overdue complaints escalated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComplaint(int id)
        {
            var success = await _apiClient.DeleteComplaintAsync(id);
            if (success)
            {
                TempData["SuccessMessage"] = $"Complaint ID {id} and associated history have been deleted via Web API.";
            }
            else
            {
                TempData["ErrorMessage"] = $"Failed to delete complaint ID {id}.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PurgePastComplaints(int daysOlderThan = 30)
        {
            int count = await _apiClient.PurgeComplaintsAsync(daysOlderThan);
            if (count > 0)
            {
                TempData["SuccessMessage"] = $"Successfully purged {count} past records older than {daysOlderThan} days via Web API.";
            }
            else
            {
                TempData["InfoMessage"] = $"No past closed/resolved records found older than {daysOlderThan} days.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
