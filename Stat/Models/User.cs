using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stat.Models
{
 
    public class User
    {
        [Key]
        
        public string ID_User { get; set; } 
        public string? User_name { get; set; }

        [MaxLength(100)]
        public string? Password { get; set; }

        

        [MaxLength(100)]
        public string? motdepasse { get; set; }
        [NotMapped]
        public bool KeepLoggedIn { get; set; }

        [MaxLength(100)]
        public string? FirstNmUser { get; set; }

        [MaxLength(100)]
        public string? LastNmUser { get; set; }

        [Required(ErrorMessage = "L'email est requis")]   
        [EmailAddress(ErrorMessage = "Adresse email invalide")]
        public string? MailUser { get; set; }

        [MaxLength(15)]
        [Required(ErrorMessage = "Le numéro de téléphone est obligatoire")]
        [Phone(ErrorMessage = "Le numéro de téléphone n’est pas valide")]
        public string? TelUser { get; set; }
          
        public string? CodeDIW { get; set; }
        public Nullable<DateTime> LastCnx { get; set; }
        public DateTime? DateDeCreation { get; set; }
          
        public DateTime? Date_deb_Affect { get; set; }
        public DateTime? Date_Fin_Affect { get; set; }
        public int Statut { get; set; } 
        public bool IsActive { get; set; }
        public string? SessionToken { get; set; }

        public string? ProfilePictureUrl { get; set; }
        public string? CreatedByUserId { get; set; }

        public virtual ICollection<UserPermission> UserPermissions { get; set; }
        [ForeignKey("CreatedByUserId")]
        public virtual User Creator { get; set; }

    }
 

}
