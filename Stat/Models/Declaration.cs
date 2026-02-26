// File: Declaration.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stat.Models
{
    public class Declaration
    {
        [Key]
        public int ID_Detail { get; set; }

        public string IDSituation { get; set; }
        
        public string IdIn { get; set; }
        public float? Cible { get; set; }
        public float? taux { get; set; }
        public float? ecart { get; set; }
        public float? Numerateur { get; set; }
        public float? Denominateur { get; set; }

        [ForeignKey("IDSituation")]
        public Situation Situation { get; set; }
        [ForeignKey("IdIn")]
        public virtual Indicateur Indicateur { get; set; }
    }
}