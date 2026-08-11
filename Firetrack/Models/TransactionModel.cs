using System;

namespace Firetrack.Models
{
    public class TransactionModel
    {
        public int TransactionId { get; set; }
        public string EquipmentQR { get; set; } = string.Empty;
        public string FromUser { get; set; } = string.Empty;
        public string ToUser { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? Remarks { get; set; }
    }
}