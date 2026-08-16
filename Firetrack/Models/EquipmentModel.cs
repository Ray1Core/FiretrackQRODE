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
        public string? RequestedByUsername { get; set; }
        public string? RequestStatus { get; set; }

        // NEW: Disposal fields
        public bool IsDisposalRequested { get; set; }
        public string? DisposalStatus { get; set; } // "Pending", "Approved", "Rejected"
        public string? DisposalReason { get; set; }
        public string? DisposalRequestedBy { get; set; }
        public DateTime? DisposalRequestDate { get; set; }
        public string? DisposalApprovedBy { get; set; }
        public DateTime? DisposalApprovalDate { get; set; }
        public string? DisposalRemarks { get; set; }
    }
}