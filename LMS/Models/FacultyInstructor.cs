using System;
using System.Collections.Generic;

namespace LMS.Models;

public partial class FacultyInstructor
{
    public int FacultyInstructorId { get; set; }

    public int FacultyId { get; set; }

    public int InstructorId { get; set; }

    public string Position { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Faculty Faculty { get; set; } = null!;

    public virtual User Instructor { get; set; } = null!;
}
