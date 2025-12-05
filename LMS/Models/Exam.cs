using System;
using System.Collections.Generic;

namespace LMS.Models;

public partial class Exam
{
    public int ExamId { get; set; }

    public int ClassId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int TotalQuestions { get; set; }

    public int Duration { get; set; }

    public decimal MaxScore { get; set; }

    public string? Conditions { get; set; }

    public bool? IsPublished { get; set; }

    public bool? AllowMultipleAttempts { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();

    public virtual ICollection<ExamResult> ExamResults { get; set; } = new List<ExamResult>();
}
