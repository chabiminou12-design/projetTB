
    using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations.Schema;


namespace Stat.Models
{
    public class UserFormViewModel
    {
        [NotMapped]
            public string? ID_User { get; set; }
            [NotMapped]
            public string? Password { get; set; }
            public string FirstNmUser { get; set; }
            public string LastNmUser { get; set; }
           public string? MailUser { get; set; }
           public string? TelUser { get; set; }
            public int SelectedDriId { get; set; }
            public int SelectedDiwId { get; set; }

            public IEnumerable<SelectListItem> DriList { get; set; }
            public IEnumerable<SelectListItem> DiwList { get; set; }
            [NotMapped]
            public DateTime DateCrea { get; set; }
            [NotMapped]
            public DateTime Date_deb_Affect { get; set; }
            [NotMapped]
            public DateTime? Date_Fin_Affect { get; set; }
            public List<User> Users { get; set; } = new();
        public User NewUser { get; set; } // Pour le formulaire d'ajout
        public List<User> ListeUsers { get; set; } // Pour la liste affichée
        public string SelectedCodeDRI { get; set; } // pour garder la DRI sélectionnée


        public Indicateur newIND { get; set; } // Pour le formulaire d'ajout
        public string? IdCategIn { get; set; }
    }
}


