using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Firetrack.Models
{
    public class DamageReportModel
    {
        public int ReportId { get; set; }
        public int EquipmentId { get; set; }
        public int ReportedBy { get; set; }
        public DateTime IncidentDate { get; set; }
        public string DamageDescription { get; set; } = string.Empty;
        public string ReportStatus { get; set; } = "Reported";
        public DateTime CreatedAt { get; set; }
    }
}