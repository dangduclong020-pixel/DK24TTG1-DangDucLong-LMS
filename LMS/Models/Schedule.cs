using System;
using System.Collections.Generic;

namespace LMS.Models;

public partial class Schedule
{
    public int ScheduleId { get; set; }

    public int ClassId { get; set; }

    public int? LessonId { get; set; }

    public string? DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string? Room { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual Lesson? Lesson { get; set; }
}
