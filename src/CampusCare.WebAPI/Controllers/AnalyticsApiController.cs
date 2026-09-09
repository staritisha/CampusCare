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
// this file contains the AnalyticsApiController class which provides endpoints for retrieving analytics data for admin, manager, staff, and student dashboards.
// It computes various metrics such as complaint counts, resolution times, feedback ratings, and workload breakdowns based on the user's role and department.
namespace CampusCare.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AnalyticsApiController : ControllerBase
    {
        private readonly IComplaintRepository _complaintRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public AnalyticsApiController(
            IComplaintRepository complaintRepository,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _complaintRepository = complaintRepository;
            _userManager = userManager;
            _context = context;
        }

        // Computes executive KPI summary, resolution time, feedback ratings, and department/category stats.
        [HttpGet("admin")]
        public async Task<IActionResult> GetAdminAnalytics()
        {
            var complaints = (await _complaintRepository.GetAllAsync()).ToList();

            int total = complaints.Count;
            int pending = complaints.Count(c => c.Status == ComplaintStatus.Submitted || c.Status == ComplaintStatus.Assigned);
            int inProgress = complaints.Count(c => c.Status == ComplaintStatus.InProgress);
            int escalated = complaints.Count(c => c.IsEscalated || c.Status == ComplaintStatus.Escalated);
            int resolved = complaints.Count(c => c.Status == ComplaintStatus.Resolved || c.Status == ComplaintStatus.Closed);

            var resolvedComplaints = complaints.Where(c => c.ResolvedAt.HasValue).ToList();
            double avgResHours = resolvedComplaints.Any()
                ? Math.Round(resolvedComplaints.Average(c => (c.ResolvedAt!.Value - c.CreatedAt).TotalHours), 1)
                : 0.0;

            var feedbacks = await _context.Feedbacks.ToListAsync();
            double avgRating = feedbacks.Any() ? Math.Round(feedbacks.Average(f => f.Rating), 1) : 5.0;

            var departments = await _context.Departments.ToListAsync();
            var deptStats = departments.Select(d => new DepartmentStatDto
            {
                DepartmentName = d.Name,
                TotalCount = complaints.Count(c => c.DepartmentId == d.Id),
                ResolvedCount = complaints.Count(c => c.DepartmentId == d.Id && (c.Status == ComplaintStatus.Resolved || c.Status == ComplaintStatus.Closed)),
                EscalatedCount = complaints.Count(c => c.DepartmentId == d.Id && (c.IsEscalated || c.Status == ComplaintStatus.Escalated))
            }).ToList();

            var categories = await _context.ComplaintCategories.ToListAsync();
            var catStats = categories.Select(c => new CategoryStatDto
            {
                CategoryName = c.Name,
                TotalCount = complaints.Count(comp => comp.CategoryId == c.Id)
            }).ToList();

            var dto = new AdminAnalyticsDto
            {
                TotalComplaints = total,
                PendingComplaints = pending,
                InProgressComplaints = inProgress,
                EscalatedComplaints = escalated,
                ResolvedComplaints = resolved,
                AverageResolutionHours = avgResHours,
                AverageFeedbackRating = avgRating,
                DepartmentStats = deptStats,
                CategoryStats = catStats
            };

            return Ok(dto);
        }

      
        // Computes department-specific metrics and technician workload breakdown for manager console.
        [HttpGet("manager/{departmentId}")]
        public async Task<IActionResult> GetManagerAnalytics(int departmentId)
        {
            var department = await _context.Departments.FindAsync(departmentId);
            var deptComplaints = (await _complaintRepository.GetByDepartmentIdAsync(departmentId)).ToList();

            var staffMembers = await _context.Users
                .Where(u => u.DepartmentId == departmentId && u.IsActive)
                .ToListAsync();

            var staffWorkload = new List<StaffWorkloadDto>();
            foreach (var staff in staffMembers)
            {
                if (await _userManager.IsInRoleAsync(staff, "Staff") || await _userManager.IsInRoleAsync(staff, "Manager"))
                {
                    var staffComplaints = deptComplaints.Where(c => c.AssignedStaffId == staff.Id);
                    staffWorkload.Add(new StaffWorkloadDto
                    {
                        StaffId = staff.Id,
                        StaffName = staff.FullName,
                        Email = staff.Email ?? string.Empty,
                        ActiveAssignedCount = staffComplaints.Count(c => c.Status == ComplaintStatus.Assigned || c.Status == ComplaintStatus.InProgress || c.Status == ComplaintStatus.Escalated),
                        ResolvedCount = staffComplaints.Count(c => c.Status == ComplaintStatus.Resolved || c.Status == ComplaintStatus.Closed)
                    });
                }
            }

            var dto = new ManagerAnalyticsDto
            {
                DepartmentId = departmentId,
                DepartmentName = department?.Name ?? "Department",
                TotalDepartmentComplaints = deptComplaints.Count,
                UnassignedCount = deptComplaints.Count(c => string.IsNullOrEmpty(c.AssignedStaffId)),
                InProgressCount = deptComplaints.Count(c => c.Status == ComplaintStatus.InProgress),
                EscalatedCount = deptComplaints.Count(c => c.IsEscalated || c.Status == ComplaintStatus.Escalated),
                ResolvedCount = deptComplaints.Count(c => c.Status == ComplaintStatus.Resolved || c.Status == ComplaintStatus.Closed),
                StaffWorkload = staffWorkload
            };

            return Ok(dto);
        }


        // Computes technician workdesk counts.
        
        [HttpGet("staff/{staffId}")]
        public async Task<IActionResult> GetStaffAnalytics(string staffId)
        {
            var complaints = (await _complaintRepository.GetByAssignedStaffIdAsync(staffId)).ToList();

            var dto = new StaffAnalyticsDto
            {
                StaffId = staffId,
                TotalAssigned = complaints.Count,
                PendingAction = complaints.Count(c => c.Status == ComplaintStatus.Assigned),
                InProgress = complaints.Count(c => c.Status == ComplaintStatus.InProgress),
                Escalated = complaints.Count(c => c.Status == ComplaintStatus.Escalated || c.IsEscalated),
                Resolved = complaints.Count(c => c.Status == ComplaintStatus.Resolved || c.Status == ComplaintStatus.Closed)
            };

            return Ok(dto);
        }


        // Computes student dashboard complaint counters.

        [HttpGet("student/{studentId}")]
        public async Task<IActionResult> GetStudentAnalytics(string studentId)
        {
            var complaints = (await _complaintRepository.GetByStudentIdAsync(studentId)).ToList();

            var dto = new StudentAnalyticsDto
            {
                StudentId = studentId,
                TotalComplaints = complaints.Count,
                PendingComplaints = complaints.Count(c => c.Status == ComplaintStatus.Submitted || c.Status == ComplaintStatus.Assigned),
                InProgressComplaints = complaints.Count(c => c.Status == ComplaintStatus.InProgress || c.Status == ComplaintStatus.Escalated),
                ResolvedComplaints = complaints.Count(c => c.Status == ComplaintStatus.Resolved || c.Status == ComplaintStatus.Closed)
            };

            return Ok(dto);
        }
    }
}
