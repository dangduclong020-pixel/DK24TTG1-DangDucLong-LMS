using System;
using System.Collections.Generic;

namespace LMS.Models;

public partial class LessonFile
{
    public int FileId { get; set; }

    public int LessonId { get; set; }

    public string FileName { get; set; } = null!;

    public string FilePath { get; set; } = null!;

    public int? FileSize { get; set; }

    public string? FileType { get; set; }

    public string? Mimetype { get; set; }

    public string? Checksum { get; set; }

    public int? FileVersion { get; set; }

    public string? Description { get; set; }

    public DateTime? UploadedAt { get; set; }

    public virtual Lesson Lesson { get; set; } = null!;
}
