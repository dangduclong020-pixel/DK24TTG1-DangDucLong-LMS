using System;
using System.Collections.Generic;

namespace LMS.Models;

public partial class DifficultyLevel
{
    public int LevelId { get; set; }

    public string LevelName { get; set; } = null!;

    public virtual ICollection<QuestionBank> QuestionBanks { get; set; } = new List<QuestionBank>();
}
