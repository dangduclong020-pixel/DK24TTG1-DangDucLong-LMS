using System;
using System.Collections.Generic;

namespace LMS.Models;

public partial class Assignment
{
    public int AssignmentId { get; set; }

    public int ClassId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string? FilePath { get; set; }

    public DateTime DueDate { get; set; }

    public int MaxScore { get; set; }

    public int? MaxAttempts { get; set; }

    public bool? AllowLateSubmission { get; set; }

    public decimal? LatePenalty { get; set; }

    public decimal? Weightage { get; set; }

    public bool? IsPublished { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();
}
