// File: Situation.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stat.Models
{
    public class Situation
    {
        [Key]
        public string IDSituation { get; set; }

        public string? DIW { get; set; }
        public string? Month { get; set; }
        public string? Year { get; set; } // Year should be an integer for correct sorting
        public int Statut { get; set; } // Statut is an integer (0 = En cours, 1 = Confirmé)

        public DateTime? CreateDate { get; set; }
        public DateTime? EditDate { get; set; }
        public DateTime? DeleteDate { get; set; }
        public DateTime? ConfirmDate { get; set; }

        [Column("ID_User")]
        public string? User_id { get; set; }
       
        [ForeignKey("DIW")]
        public virtual DIW DIWNavigation { get; set; }
        [ForeignKey("User_id")]
        public virtual User User { get; set; }
        public virtual ICollection<RejectionHistory> RejectionHistories { get; set; }

        public DateTime? DRIValidationDate { get; set; }
        public DateTime? AdminValidationDate { get; set; }
    }
}