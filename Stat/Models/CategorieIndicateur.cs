using System.ComponentModel.DataAnnotations;

namespace Stat.Models
{
    public class CategorieIndicateur
    {
        [Key]
        public string? IdCategIn { get; set; }
        public string? IntituleCategIn { get; set; }
        public virtual ICollection<Indicateur> Indicateurs { get; set; }

        public virtual ICollection<Objectif> Objectifs { get; set; }

    }
}
