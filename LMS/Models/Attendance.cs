using System;
using System.Collections.Generic;

namespace LMS.Models;

/// <summary>
/// Phiên điểm danh - Được tạo bởi giảng viên cho mỗi buổi học
/// </summary>
public partial class Attendance
{
    public int AttendanceId { get; set; }

    public int ClassId { get; set; }

    public int? LessonId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    /// <summary>
    /// Mã code để sinh viên điểm danh (6 ký tự ngẫu nhiên)
    /// </summary>
    public string? AttendanceCode { get; set; }

    /// <summary>
    /// Cho phép điểm danh bằng code
    /// </summary>
    public bool AllowCodeAttendance { get; set; }

    /// <summary>
    /// Tự động đóng khi hết thời gian
    /// </summary>
    public bool AutoClose { get; set; }

    /// <summary>
    /// Trạng thái: Open, Closed
    /// </summary>
    public string Status { get; set; } = "Open";

    public int CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    // Navigation properties
    public virtual Class Class { get; set; } = null!;

    public virtual Lesson? Lesson { get; set; }

    public virtual User Creator { get; set; } = null!;

    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}
