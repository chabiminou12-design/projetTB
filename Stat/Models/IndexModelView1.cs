using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stat.Models
{
    public class IndexModelView1
    {       
        //public IEnumerable<User>? Users { get; set; }
        //public IEnumerable<Situation>? Situations { get; set; }
        //public string? IDSituation { get; set; }
        public string? ID_User { get; set; }
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public bool KeepLoggedIn { get; set; }
        public DateTime LastCnx { get; set; }
        public string? MailUser { get; set; }
        public string? PrenomUser { get; set; }
        public string? TelUser { get; set; }
        public DateTime DateDeCreation { get; set; }
        public string? CodeDIW { get; set; }

        //public String? LibelleDIW { get; set; }
        //public DIW? DIW { get; set; }
        public string? CodeDRI { get; set; }
        public DateTime? Date_debut_Affectation { get; set; }
        public DateTime? Date_Fin_Affectation { get; set; }
        public string SelectedDRI { get; set; }
        public string SelectedDIW { get; set; }
        //public List<DRI> DRIs { get; set; }
        public List<DIW> DIWs { get; set; }   // Liste des DIW
        public List<User> Users { get; set; } = new();
        public List<DRI> DRIs { get; set; } = new();
    }
}