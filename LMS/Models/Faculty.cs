using System;
using System.Collections.Generic;

namespace LMS.Models;

public partial class Faculty
{
    public int FacultyId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public int? DeanId { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? ImagePath { get; set; }

    public virtual ICollection<Course> Courses { get; set; } = new List<Course>();

    public virtual ICollection<Department> Departments { get; set; } = new List<Department>();

    public virtual ICollection<FacultyInstructor> FacultyInstructors { get; set; } = new List<FacultyInstructor>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
