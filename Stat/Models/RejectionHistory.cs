// In Models/RejectionHistory.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stat.Models
{
    public class RejectionHistory
    {
        [Key]
        public int Id { get; set; }
        public string Comment { get; set; }
        public DateTime RejectionDate { get; set; }
        public string RejectedByUserId { get; set; } // The DRI's ID

        public string IDSituation { get; set; } // Foreign Key
        [ForeignKey("IDSituation")]
        public virtual Situation Situation { get; set; }
    }
}