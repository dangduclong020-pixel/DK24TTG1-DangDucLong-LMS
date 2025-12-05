using System;
using System.Collections.Generic;

namespace LMS.Models;

public partial class ClassStudent
{
    public int ClassId { get; set; }

    public int StudentId { get; set; }

    public DateTime? EnrollDate { get; set; }

    public string? Status { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual User Student { get; set; } = null!;
}
