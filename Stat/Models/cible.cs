using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stat.Models
{
    [Table("cibles")]
    public class Cible
    {
        [Key]
        public long id_cible { get; set; }
        
        public string CodeDIW { get; set; }

        public string IdIn { get; set; }

        public float cible { get; set; }

        public string year { get; set; }
    }
}