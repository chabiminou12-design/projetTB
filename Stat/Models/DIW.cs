using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection.Metadata.Ecma335;

namespace Stat.Models
{

    public class DIW
    {
        [Key]
        [MaxLength(7)]
        public string CodeDIW { get; set; } 

        [MaxLength(100)]
        public string? LibelleDIW { get; set; }
        public string CodeDRI { get; set; }
        [ForeignKey("CodeDRI")]
        public virtual DRI DRI { get; set; }


    }

}
