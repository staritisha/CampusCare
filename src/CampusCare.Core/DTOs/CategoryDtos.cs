using System;
using System.Collections.Generic;

namespace CampusCare.Core.DTOs
{
    public class CreateCategoryDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DefaultDepartmentId { get; set; }
    }

    public class UpdateCategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DefaultDepartmentId { get; set; }
    }

    public class CategoryDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DefaultDepartmentId { get; set; }
        public string? DefaultDepartmentName { get; set; }
    }
}
