using System;
using System.Collections.Generic;

namespace LMS.Models;

public partial class AdministrativeClass
{
    public int AdministrativeClassId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int FacultyId { get; set; }

    public int? DepartmentId { get; set; }

    public string AcademicYear { get; set; } = null!;

    public string? Intake { get; set; }

    public int? MaxStudents { get; set; }

    public int? CurrentStudents { get; set; }

    public int? AdvisorId { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual User? Advisor { get; set; }

    public virtual Department? Department { get; set; }

    public virtual Faculty Faculty { get; set; } = null!;

    public virtual ICollection<User> Students { get; set; } = new List<User>();
}
