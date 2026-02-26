using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Stat.Models
{
    [Table("cibles_de_performance_dri")]
    public class cibles_de_performance_dri
    {
        [Key]
        public int Id_cible { get; set; }
        [ForeignKey("IdIndicacteur")]
        public int IdIndicacteur { get; set; }

        public double cible { get; set; }

        public string year { get; set; }
        public string CodeDRI { get; set; }
        [ForeignKey("IdIndicacteur")]
        public virtual Indicateurs_DE_PERFORMANCE_OPERATIONNELS Indicateurs_DE_PERFORMANCE_OPERATIONNELS { get; set; }
        [ForeignKey("CodeDRI")]
        public virtual DRI DRI { get; set; }
    }
}
