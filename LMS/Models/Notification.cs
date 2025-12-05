using System;
using System.Collections.Generic;

namespace LMS.Models;

public partial class Notification
{
    public int NotificationId { get; set; }

    public int SenderId { get; set; }

    public int ReceiverId { get; set; }

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string? Type { get; set; }

    public bool? IsRead { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User Receiver { get; set; } = null!;

    public virtual User Sender { get; set; } = null!;
}
