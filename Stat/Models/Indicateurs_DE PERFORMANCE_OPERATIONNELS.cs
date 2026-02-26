using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Stat.Models
    
{
    [Table("Indicateurs_DE_PERFORMANCE_OPERATIONNELS")]
    public class Indicateurs_DE_PERFORMANCE_OPERATIONNELS
    {
        [Key]
        public int IdIndicacteur { get; set; }
        public string? IntituleIn { get; set; }
        public string? IdCategIn { get; set; }
    }
}
