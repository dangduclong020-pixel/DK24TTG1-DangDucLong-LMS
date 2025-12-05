using System;
using System.Collections.Generic;

namespace LMS.Models;

public partial class ExamQuestion
{
    public int ExamId { get; set; }

    public int QuestionId { get; set; }

    public int? QuestionOrder { get; set; }

    public decimal? QuestionScore { get; set; }

    public virtual Exam Exam { get; set; } = null!;

    public virtual QuestionBank Question { get; set; } = null!;
}
