// File: Stat/Models/DeclarationDRI.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stat.Models
{
    [Table("declarations_dri")]
    public class DeclarationDRI
    {
        [Key]
        public int ID_Detail_DRI { get; set; }

        [Required]
        public string IDSituation { get; set; }

        [Required]
        public int IdIndicacteur { get; set; } // Foreign key to the DRI indicators table

        public double? Cible { get; set; }
        public double? taux { get; set; }
        public double? ecart { get; set; }
        public double? Numerateur { get; set; }
        public double? Denominateur { get; set; }

        // Navigation properties
        [ForeignKey("IDSituation")]
        public virtual Situation Situation { get; set; }

        [ForeignKey("IdIndicacteur")]
        public virtual Indicateurs_DE_PERFORMANCE_OPERATIONNELS Indicateur { get; set; }
    }
}