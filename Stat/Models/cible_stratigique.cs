// [File: cible_stratigique.cs]
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stat.Models
{
    [Table("cibles_stratigiques")]
    public class cible_stratigique
    {
        [Key]
        public long id_cible { get; set; }

        [ForeignKey("IndicateurStrategique")]
        public string IdIndic { get; set; }

        public double cible { get; set; }

        public string year { get; set; }

        public virtual IndicateurStrategique IndicateurStrategique { get; set; }
    }
}