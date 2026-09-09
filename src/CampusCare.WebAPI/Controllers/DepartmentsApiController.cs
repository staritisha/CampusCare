using CampusCare.Core.DTOs;
using CampusCare.Core.Entities;
using CampusCare.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CampusCare.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DepartmentsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DepartmentsApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }


        /// Gets all departments with staff and categories included.
        // path for api is /api/departmentsapi
        [HttpGet]
        public async Task<IActionResult> GetDepartments()
        {
            var departments = await _context.Departments
                .Include(d => d.StaffMembers)
                .Include(d => d.Categories)
                .ToListAsync();

            return Ok(departments);
        }


        /// Gets department details by ID.
        // path for api is /api/departmentsapi/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetDepartment(int id)
        {
            var department = await _context.Departments
                .Include(d => d.StaffMembers)
                .Include(d => d.Categories)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (department == null) return NotFound(new { Message = $"Department ID {id} not found." });
            return Ok(department);
        }


        /// Creates a new department.
        //path for api is /api/departmentsapi

        [HttpPost]
        public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Name) || string.IsNullOrWhiteSpace(model.Code))
            {
                return BadRequest(new { Message = "Name and Code are required." });
            }

            var dept = new Department
            {
                Name = model.Name.Trim(),
                Code = model.Code.Trim().ToUpper(),
                Description = model.Description?.Trim()
            };

            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetDepartment), new { id = dept.Id }, dept);
        }


        /// Updates an existing department.
        /// path for api is /api/departmentsapi/{id}

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDepartment(int id, [FromBody] UpdateDepartmentDto model)
        {
            var dept = await _context.Departments.FindAsync(id);
            if (dept == null) return NotFound(new { Message = $"Department ID {id} not found." });

            dept.Name = model.Name.Trim();
            dept.Code = model.Code.Trim().ToUpper();
            dept.Description = model.Description?.Trim();

            await _context.SaveChangesAsync();
            return Ok(dept);
        }

        
        /// Deletes a department by ID.
        
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDepartment(int id)
        {
            var dept = await _context.Departments.FindAsync(id);
            if (dept == null) return NotFound(new { Message = $"Department ID {id} not found." });

            _context.Departments.Remove(dept);
            await _context.SaveChangesAsync();
            return Ok(new { Success = true, Message = $"Department '{dept.Name}' deleted." });
        }


        /// Gets active staff members in the specified department.
        /// path    for api is /api/departmentsapi/{id}/staff

        [HttpGet("{id}/staff")]
        public async Task<IActionResult> GetDepartmentStaff(int id)
        {
            var users = await _context.Users
                .Where(u => u.DepartmentId == id && u.IsActive)
                .ToListAsync();

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
    }
}
