using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stat.Models
{

    public class DRI
    {
        [Key]
        [MaxLength(7)]
        public string CodeDRI { get; set; } 
        [MaxLength(100)]
        public string? LibelleDRI { get; set; }

        public virtual ICollection<DIW> DIWs { get; set; }



    }
}
