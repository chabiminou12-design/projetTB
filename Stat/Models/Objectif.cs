// Dans Models/Objectif.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stat.Models
{
    public class Objectif
    {
        [Key]
        public int idobj { get; set; }

        [MaxLength(200)]
        public string Intituleobj { get; set; }

        public string IdCategIn { get; set; }

        // --- Propriétés de navigation ---
        [ForeignKey("IdCategIn")]
        public virtual CategorieIndicateur CategorieIndicateur { get; set; }

        public virtual ICollection<IndicateurStrategique> IndicateursStrategiques { get; set; }
    }
}