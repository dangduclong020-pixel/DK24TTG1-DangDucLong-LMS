using System;
using System.Collections.Generic;

namespace LMS.Models;

public partial class ExamResult
{
    public int ResultId { get; set; }

    public int ExamId { get; set; }

    public int StudentId { get; set; }

    public decimal? Score { get; set; }

    public decimal TotalScore { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? Status { get; set; }

    public int? AttemptNumber { get; set; }

    public virtual Exam Exam { get; set; } = null!;

    public virtual ICollection<ExamAnswer> ExamAnswers { get; set; } = new List<ExamAnswer>();

    public virtual User Student { get; set; } = null!;
}
