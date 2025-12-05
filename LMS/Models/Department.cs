using System;
using System.Collections.Generic;

namespace LMS.Models;

public partial class Department
{
    public int DepartmentId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public int FacultyId { get; set; }

    public int? HeadId { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? ImagePath { get; set; }

    public virtual Faculty Faculty { get; set; } = null!;

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
