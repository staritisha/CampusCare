using CampusCare.Core.DTOs;
using CampusCare.Core.Entities;
using CampusCare.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace CampusCare.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CategoriesApiController(ApplicationDbContext context)
        {
            _context = context;
        }

       
        //Gets all complaint categories including default department info.
        
        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.ComplaintCategories
                .Include(c => c.DefaultDepartment)
                .ToListAsync();

            return Ok(categories);
        }

        
        //Gets category details by ID.
       
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCategory(int id)
        {
            var category = await _context.ComplaintCategories
                .Include(c => c.DefaultDepartment)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null) return NotFound(new { Message = $"Category ID {id} not found." });
            return Ok(category);
        }

      
        // Creates a new complaint category.
        
        [HttpPost]
        public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Name))
            {
                return BadRequest(new { Message = "Category Name is required." });
            }

            var category = new ComplaintCategory
            {
                Name = model.Name.Trim(),
                Description = model.Description?.Trim(),
                DefaultDepartmentId = model.DefaultDepartmentId
            };

            _context.ComplaintCategories.Add(category);
            await _context.SaveChangesAsync();

            var created = await _context.ComplaintCategories
                .Include(c => c.DefaultDepartment)
                .FirstOrDefaultAsync(c => c.Id == category.Id);

            return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, created);
        }

        // Updates an existing category.

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdateCategoryDto model)
        {
            var category = await _context.ComplaintCategories.FindAsync(id);
            if (category == null) return NotFound(new { Message = $"Category ID {id} not found." });

            category.Name = model.Name.Trim();
            category.Description = model.Description?.Trim();
            category.DefaultDepartmentId = model.DefaultDepartmentId;

            await _context.SaveChangesAsync();
            return Ok(category);
        }


        // Deletes a category by ID.
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.ComplaintCategories.FindAsync(id);
            if (category == null) return NotFound(new { Message = $"Category ID {id} not found." });

            _context.ComplaintCategories.Remove(category);
            await _context.SaveChangesAsync();
            return Ok(new { Success = true, Message = $"Category '{category.Name}' deleted." });
        }
    }
}
