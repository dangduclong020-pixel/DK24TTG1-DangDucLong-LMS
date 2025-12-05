using System;
using System.Collections.Generic;

namespace LMS.Models;

public partial class ExamAnswer
{
    public int AnswerId { get; set; }

    public int ResultId { get; set; }

    public int QuestionId { get; set; }

    public string StudentAnswer { get; set; } = null!;

    public bool? IsCorrect { get; set; }

    public decimal? QuestionScore { get; set; }

    public DateTime? AnswerTime { get; set; }

    public virtual QuestionBank Question { get; set; } = null!;

    public virtual ExamResult Result { get; set; } = null!;
}
