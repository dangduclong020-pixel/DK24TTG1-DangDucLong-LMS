using System;
using System.Collections.Generic;

namespace LMS.Models;

public partial class Class
{
    public int ClassId { get; set; }

    public string Name { get; set; } = null!;

    public string Code { get; set; } = null!;

    public int CourseId { get; set; }

    public int InstructorId { get; set; }

    public int? MaxStudents { get; set; }

    public int? CurrentStudents { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? Description { get; set; }

    public string? Objectives { get; set; }

    public string? IntroVideoUrl { get; set; }

    public string? SlidesUrl { get; set; }

    public string? LectureVideoUrl { get; set; }

    public string? MissionTitle { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public string? Semester { get; set; }

    public string? Session { get; set; }

    public string? DayOfWeek { get; set; }

    public string? Room { get; set; }

    public int? Credits { get; set; }

    public string? CourseType { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();

    public virtual ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();

    public virtual ICollection<ClassStudent> ClassStudents { get; set; } = new List<ClassStudent>();

    public virtual Course Course { get; set; } = null!;

    public virtual ICollection<Exam> Exams { get; set; } = new List<Exam>();

    public virtual User Instructor { get; set; } = null!;

    public virtual ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();

    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();

    public virtual ICollection<StudentGrade> StudentGrades { get; set; } = new List<StudentGrade>();
}
