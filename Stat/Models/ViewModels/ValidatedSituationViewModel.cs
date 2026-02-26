using Stat.Models;
using System;

namespace Stat.Models.ViewModels
{
    public class ValidatedSituationViewModel
    {
        public string SituationId { get; set; }
        public string Period { get; set; } // e.g., "Juillet 2025"
        public string StructureName { get; set; }
        public string SubmittedBy { get; set; }
        public string SituationType { get; set; } // "Opérationnelle" or "Stratégique"
        public DateTime? ValidationDate { get; set; }
    }
}