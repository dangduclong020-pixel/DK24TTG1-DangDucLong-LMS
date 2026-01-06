using System;

namespace LMS.Models;

/// <summary>
/// Bản ghi điểm danh của từng sinh viên
/// </summary>
public partial class AttendanceRecord
{
    public int RecordId { get; set; }

    public int AttendanceId { get; set; }

    public int StudentId { get; set; }

    /// <summary>
    /// Trạng thái: Present (Có mặt), Absent (Vắng), Late (Muộn), Excused (Có phép)
    /// </summary>
    public string Status { get; set; } = "Absent";

    /// <summary>
    /// Thời gian sinh viên check-in
    /// </summary>
    public DateTime? CheckInTime { get; set; }

    /// <summary>
    /// Phương thức điểm danh: Manual (GV điểm), Code (SV nhập mã), Auto (Tự động)
    /// </summary>
    public string? CheckInMethod { get; set; }

    /// <summary>
    /// Ghi chú của giảng viên
    /// </summary>
    public string? Note { get; set; }

    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual Attendance Attendance { get; set; } = null!;

    public virtual User Student { get; set; } = null!;
}
