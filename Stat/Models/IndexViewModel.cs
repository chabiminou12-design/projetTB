using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stat.Models
{
    public class IndexViewModel
    {
        public User? Users { get; set; }
        public IEnumerable<Indicateur>? Indicateurs { get; set; }
        
        // public List<Situation> Situations { get; set; }
        public string? IDSituation { get; set; }
        [StringLength(8, MinimumLength = 8, ErrorMessage = "Year must be exactly 4 characters long.")]
        public string? DIW { get; set; }
        public string? Month { get; set; }
        [StringLength(4, MinimumLength = 4, ErrorMessage = "Year must be exactly 4 characters long.")]
         public string? Year { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime EditDate { get; set; }
        public DateTime DeleteDate { get; set; }
        public DateTime ConfirmDate { get; set; }
        public int? Statut { get; set; }
        public IEnumerable<CategorieIndicateur>? CategorieIndicateurs { get; set; }
        public string? IdCategIn { get; set; }
        public string? IntituleCategIn { get; set; }
        public Situation Situation { get; set; }  
        public IEnumerable<Situation>? Situations { get; set; } 
        public string Message { get; set; }
        public double ValeurMois { get; set; }
        public double ValeurCible { get; set; }
        public double cumule { get; set; }
        public double taux { get; set; }
        public int ligne { get; set; }


        public string? ID_User { get; set; }
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public bool KeepLoggedIn { get; set; }
        public DateTime LastCnx { get; set; }
        public string? CodeDIW { get; set; }
        public string? MailUser { get; set; }
        public string? PrenomUser { get; set; }

        public string? TelUser { get; set; }
        public DateTime DateDeCreation { get; set; }
        public List<IndicatorWithCibleViewModel> IndicatorsWithCibles { get; set; }

        public int SituationsConfirmes { get; set; }
        public int SituationsEnCours { get; set; }
        public int TotalIndicateurs { get; set; }

        // Properties for the chart data
        public List<string> BarChartLabels { get; set; }
        public List<float> BarChartData { get; set; }

    }
}

