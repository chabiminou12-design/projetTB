using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stat.Models
{
    public class DeclarationDraft
    {
        [Key]
        public int Id { get; set; }

        [Required]
        // This was Guid, it MUST be string to match the Situation's primary key.
        public string IDSituation { get; set; }

        [ForeignKey("IDSituation")]
        public virtual Situation Situation { get; set; }

        [Required]
        public string IdIn { get; set; }

        public float Cible { get; set; }
        public float? Taux { get; set; }
        public float? Ecart { get; set; }
        public float? Numerateur { get; set; }
        public float? Denominateur { get; set; }
    }
}