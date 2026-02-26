// In Models/DeclarationStrategique.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stat.Models
{
    [Table("declarations stratigique")]
    public class DeclarationStrategique
    {
        [Key]
        [Column("ID_Detail_stra")]
        public int ID_Detail_Strat { get; set; } // Assuming a new primary key

        public string IDSituation { get; set; }

        public string IdIndic { get; set; } // Foreign key to IndicateurStrategique

        public float? Cible { get; set; }
        public float? taux { get; set; }
        public float? ecart { get; set; }
        public float? Numerateur { get; set; }
        public float? Denominateur { get; set; }

        // Navigation properties
        [ForeignKey("IDSituation")]
        public virtual Situation Situation { get; set; }

        [ForeignKey("IdIndic")]
        public virtual IndicateurStrategique IndicateurStrategique { get; set; }
    }
}