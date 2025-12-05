using System;
using System.Collections.Generic;

namespace LMS.Models;

public partial class DeleteLog
{
    public int DeleteLogId { get; set; }

    public string TableName { get; set; } = null!;

    public int RecordId { get; set; }

    public int DeletedBy { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? Reason { get; set; }

    public virtual User DeletedByNavigation { get; set; } = null!;
}
