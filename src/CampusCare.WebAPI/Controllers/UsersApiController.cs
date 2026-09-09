using CampusCare.Core.DTOs;
using CampusCare.Core.Entities;
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
    public class UsersApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public UsersApiController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }


        /// Gets all users with their roles and department info.
        //path for api is /api/usersapi
        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _userManager.Users
                .Include(u => u.Department)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            var userItems = new List<UserSummaryDto>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userItems.Add(new UserSummaryDto
                {
                    UserId = user.Id,
                    FullName = user.FullName,
                    Email = user.Email ?? string.Empty,
                    Role = roles.FirstOrDefault() ?? "Student",
                    DepartmentId = user.DepartmentId,
                    DepartmentName = user.Department?.Name,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt
                });
            }

            return Ok(userItems);
        }


        /// Gets staff and managers for a specific department.
        //path for api is /api/usersapi/staff?departmentId=1
        [HttpGet("staff")]
        public async Task<IActionResult> GetStaff([FromQuery] int? departmentId)
        {
            var query = _context.Users.Where(u => u.IsActive);
            if (departmentId.HasValue && departmentId.Value > 0)
            {
                query = query.Where(u => u.DepartmentId == departmentId.Value);
            }

            var users = await query.ToListAsync();
            var staffList = new List<StaffListItemDto>();

            foreach (var user in users)
            {
                if (await _userManager.IsInRoleAsync(user, "Staff") || await _userManager.IsInRoleAsync(user, "Manager"))
                {
                    staffList.Add(new StaffListItemDto
                    {
                        Id = user.Id,
                        FullName = user.FullName,
                        Email = user.Email ?? string.Empty,
                        DepartmentId = user.DepartmentId
                    });
                }
            }

            return Ok(staffList);
        }


        /// Creates a new staff or manager user account.
        // path for api is /api/usersapi/staff
        [HttpPost("staff")]
        public async Task<IActionResult> CreateStaff([FromBody] CreateStaffUserDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.FullName))
            {
                return BadRequest(new { Success = false, Message = "Email and Full Name are required." });
            }

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                return BadRequest(new { Success = false, Message = $"User with email '{model.Email}' already exists." });
            }

            var staffUser = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                DepartmentId = model.DepartmentId,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(staffUser, model.Password);
            if (!result.Succeeded)
            {
                string errors = string.Join("; ", result.Errors.Select(e => e.Description));
                return BadRequest(new { Success = false, Message = errors });
            }

            string targetRole = (model.Role == "Manager") ? "Manager" : "Staff";
            if (!await _roleManager.RoleExistsAsync(targetRole))
            {
                await _roleManager.CreateAsync(new IdentityRole(targetRole));
            }
            await _userManager.AddToRoleAsync(staffUser, targetRole);

            return Ok(new
            {
                Success = true,
                Message = $"{targetRole} member '{model.FullName}' added successfully.",
                UserId = staffUser.Id
            });
        }


        /// Toggles a user's active status.
        // path for api is /api/usersapi/{id}/toggle-status
        [HttpPut("{id}/toggle-status")]
        public async Task<IActionResult> ToggleUserStatus(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound(new { Success = false, Message = $"User ID '{id}' not found." });

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);

            return Ok(new
            {
                Success = true,
                Message = $"Account status for {user.Email} updated to {(user.IsActive ? "Active" : "Deactivated")}.",
                IsActive = user.IsActive
            });
        }
    }
}
