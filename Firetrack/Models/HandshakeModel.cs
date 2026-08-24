using System;

namespace Firetrack.Models
{
    public class HandshakeModel
    {
        public int HandshakeId { get; set; }
        public int EquipmentId { get; set; }
        public int FromUserId { get; set; }
        public int ToUserId { get; set; }
        public DateTime TransferDate { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Accepted, Rejected
        public string? Notes { get; set; }

        // ---- Navigation properties (populated via join) ----
        public EquipmentModel? Equipment { get; set; }
        public UserModel? FromUser { get; set; }
        public UserModel? ToUser { get; set; }
    }
}