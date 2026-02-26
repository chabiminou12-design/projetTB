using System.ComponentModel.DataAnnotations;

namespace Stat.Models
{
    public class DC
    {
        [Key]
        [MaxLength(7)]
        public string CodeDC { get; set; } // Clé primaire

        [MaxLength(100)]
        public string? LibelleDC { get; set; }

        public string? description { get; set; }

    }
}

