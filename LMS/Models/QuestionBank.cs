using System;
using System.Collections.Generic;

namespace LMS.Models;

public partial class QuestionBank
{
    public int QuestionId { get; set; }

    public int CourseId { get; set; }

    public string Content { get; set; } = null!;

    public string Type { get; set; } = null!;

    public string Answer { get; set; } = null!;

    public decimal? MaxScore { get; set; }

    public int? DifficultyId { get; set; }

    public string? Options { get; set; }

    public string? Explanation { get; set; }

    public int CreatedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Course Course { get; set; } = null!;

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual DifficultyLevel? Difficulty { get; set; }

    public virtual ICollection<ExamAnswer> ExamAnswers { get; set; } = new List<ExamAnswer>();

    public virtual ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();
}
