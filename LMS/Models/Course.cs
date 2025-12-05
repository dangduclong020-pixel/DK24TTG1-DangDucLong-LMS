using System;
using System.Collections.Generic;

namespace LMS.Models;

public partial class Course
{
    public int CourseId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? Objectives { get; set; }

    public int Credits { get; set; }

    public int Duration { get; set; }

    public string? Prerequisites { get; set; }

    public int FacultyId { get; set; }

    public int LeadInstructorId { get; set; }

    public string AcademicYear { get; set; } = null!;

    public string Semester { get; set; } = null!;

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();

    public virtual Faculty Faculty { get; set; } = null!;

    public virtual User LeadInstructor { get; set; } = null!;

    public virtual ICollection<QuestionBank> QuestionBanks { get; set; } = new List<QuestionBank>();
}
