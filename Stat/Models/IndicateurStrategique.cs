using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stat.Models
{
    [Table("Indicateurs_stratigique")]
    public class IndicateurStrategique
    {
        [Key]
        [MaxLength(10)]
        public string IdIndic { get; set; }

        [MaxLength(200)]
        public string IntituleIn { get; set; }

        [MaxLength(20)]
        public string IdCategIn { get; set; }
        public int idobj { get; set; }


        [ForeignKey("IdCategIn")]
        public virtual CategorieIndicateur CategorieIndicateur { get; set; }

        [ForeignKey("idobj")]
        public virtual Objectif Objectif { get; set; }
        public virtual ICollection<cible_stratigique> CiblesStrategiques { get; set; }
    }
}