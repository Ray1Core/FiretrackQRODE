using System;

namespace Firetrack.Models
{
    public class EquipmentModel
    {
        public int EquipmentId { get; set; }
        public string QRCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = "Available";
        public string? AssignedToUsername { get; set; }
        public string? PhotoPath { get; set; }
        public string? Remarks { get; set; }
        public DateTime? LastUpdated { get; set; }
        public string? RequestedByUsername { get; set; }   // NEW
        public string? RequestStatus { get; set; }         // NEW: "Pending", "Approved", "Rejected"
    }
}