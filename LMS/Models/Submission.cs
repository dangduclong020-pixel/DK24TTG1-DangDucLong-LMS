using System;
using System.Collections.Generic;

namespace LMS.Models;

public partial class Submission
{
    public int SubmissionId { get; set; }

    public int AssignmentId { get; set; }

    public int StudentId { get; set; }

    public string FilePath { get; set; } = null!;

    public string FileName { get; set; } = null!;

    public string? Mimetype { get; set; }

    public string? Checksum { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public decimal? Score { get; set; }

    public string? Feedback { get; set; }

    public int? AttemptNumber { get; set; }

    public string? Status { get; set; }

    public virtual Assignment Assignment { get; set; } = null!;

    public virtual User Student { get; set; } = null!;
}
