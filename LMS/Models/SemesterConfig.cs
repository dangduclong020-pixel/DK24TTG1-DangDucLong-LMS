using System;

namespace LMS.Models;

public partial class SemesterConfig
{
    public int SemesterConfigId { get; set; }

    public string AcademicYear { get; set; } = null!;

    public int SemesterNumber { get; set; }

    public string SemesterName { get; set; } = null!;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public int DurationMonths { get; set; }

    public int DurationWeeks { get; set; }

    public bool IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
