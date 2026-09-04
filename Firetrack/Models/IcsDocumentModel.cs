using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Firetrack.Models
{
    public class IcsDocumentModel
    {
        public int IcsId { get; set; }
        public int EquipmentId { get; set; }
        public int IssuedTo { get; set; }
        public string IcsNumber { get; set; } = string.Empty;
        public DateTime DateIssued { get; set; }
        public string? DocumentPath { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}