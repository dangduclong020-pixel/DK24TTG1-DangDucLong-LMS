using System;
using System.Collections.Generic;

namespace LMS.Models;

public partial class Lesson
{
    public int LessonId { get; set; }

    public int ClassId { get; set; }

    public string Title { get; set; } = null!;

    public string? Content { get; set; }

    public int Week { get; set; }

    public int OrderNumber { get; set; }

    public int? Duration { get; set; }

    public bool? IsPublished { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual ICollection<LessonFile> LessonFiles { get; set; } = new List<LessonFile>();

    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
}
