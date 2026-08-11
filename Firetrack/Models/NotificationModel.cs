using System;

namespace Firetrack.Models
{
    public class NotificationModel
    {
        public int NotificationId { get; set; }
        public string Username { get; set; } = string.Empty;   // recipient
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime Timestamp { get; set; }
    }
}