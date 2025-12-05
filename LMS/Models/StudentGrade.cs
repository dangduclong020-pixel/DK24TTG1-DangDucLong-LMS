using System;
using System.Collections.Generic;

namespace LMS.Models;

public partial class StudentGrade
{
    public int GradeId { get; set; }

    public int StudentId { get; set; }

    public int ClassId { get; set; }

    public decimal? FinalScore { get; set; }

    public string? GradeLetter { get; set; }

    public DateTime? CalculatedAt { get; set; }

    public string? Status { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual User Student { get; set; } = null!;
}
