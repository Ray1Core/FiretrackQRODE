using System;

namespace Firetrack.Models
{
    public class EquipmentModel
    {
        // ---- New schema columns ----
        public int EquipmentId { get; set; }
        public string PropertyNumber { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? SerialNumber { get; set; }
        public DateTime? AcquisitionDate { get; set; }
        public decimal? AcquisitionCost { get; set; }
        public string ConditionStatus { get; set; } = "Serviceable";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // ---- Legacy / backward compatibility properties ----
        // These ARE now persisted in the Equipment table (added in DatabaseService).
        public string? AssignedToUsername { get; set; }
        public string? RequestedByUsername { get; set; }
        public string? RequestStatus { get; set; }
        public bool IsDisposalRequested { get; set; } = false;
        public string? DisposalStatus { get; set; }
        public string? DisposalReason { get; set; }
        public string? DisposalRequestedBy { get; set; }
        public DateTime? DisposalRequestDate { get; set; }
        public string? DisposalApprovedBy { get; set; }
        public DateTime? DisposalApprovalDate { get; set; }
        public string? DisposalRemarks { get; set; }
        public string? PhotoPath { get; set; }

        // ✅ FIX: LastUpdated is now an alias for UpdatedAt so both stay in sync
        public DateTime? LastUpdated
        {
            get => UpdatedAt == default ? null : UpdatedAt;
            set { if (value.HasValue) UpdatedAt = value.Value; }
        }

        // ---- Aliases for old property names (used in XAML and code) ----
        public string QRCode
        {
            get => PropertyNumber;
            set => PropertyNumber = value;
        }
        public string Name
        {
            get => ItemName;
            set => ItemName = value;
        }
        public string Type
        {
            get => Category;
            set => Category = value;
        }
        public string Status
        {
            get => ConditionStatus;
            set => ConditionStatus = value;
        }
        public string? Remarks
        {
            get => Description;
            set => Description = value;
        }
    }
}