using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Stat.Models
{
    public class Indicateur
    {
        [Key]
        [MaxLength(20)]
        public string? IdIn { get; set; }
        public string? IntituleIn { get; set; }
        public string? IdCategIn { get; set; }

        [ForeignKey("IdCategIn")]
        public virtual CategorieIndicateur CategorieIndicateur { get; set; }

        // Also add this to define the other side of the relationship (optional but good practice)
        public virtual ICollection<Declaration> Declarations { get; set; }

    }
}
