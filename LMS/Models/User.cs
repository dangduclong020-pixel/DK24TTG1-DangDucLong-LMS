using System;
using System.Collections.Generic;

namespace LMS.Models;

public partial class User
{
    public int UserId { get; set; }

    public string MssvMgv { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public int? RoleId { get; set; }

    public string? Status { get; set; }

    public int? FacultyId { get; set; }

    public int? DepartmentId { get; set; }

    public string? StudentClass { get; set; }

    public string? Phone { get; set; }

    public string? Avatar { get; set; }

    public DateTime? LastLogin { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();

    public virtual ICollection<ClassStudent> ClassStudents { get; set; } = new List<ClassStudent>();

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();

    public virtual ICollection<Course> Courses { get; set; } = new List<Course>();

    public virtual ICollection<DeleteLog> DeleteLogs { get; set; } = new List<DeleteLog>();

    public virtual Department? Department { get; set; }

    public virtual ICollection<ExamResult> ExamResults { get; set; } = new List<ExamResult>();

    public virtual Faculty? Faculty { get; set; }

    public virtual ICollection<FacultyInstructor> FacultyInstructors { get; set; } = new List<FacultyInstructor>();

    public virtual ICollection<Notification> NotificationReceivers { get; set; } = new List<Notification>();

    public virtual ICollection<Notification> NotificationSenders { get; set; } = new List<Notification>();

    public virtual ICollection<QuestionBank> QuestionBanks { get; set; } = new List<QuestionBank>();

    public virtual Role? Role { get; set; }

    public virtual ICollection<StudentGrade> StudentGrades { get; set; } = new List<StudentGrade>();

    public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();
}
