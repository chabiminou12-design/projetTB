using System.ComponentModel.DataAnnotations;

namespace Stat.Models
{
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Le mot de passe actuel est requis.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mot de passe actuel")]
        public string OldPassword { get; set; }

        [Required(ErrorMessage = "Le nouveau mot de passe est requis.")]
        [DataType(DataType.Password)]
        [Display(Name = "Nouveau mot de passe")]
        // ✨ CORRECTED VALIDATION RULES
        [StringLength(100, ErrorMessage = "Le {0} doit comporter au moins {2} caractères.", MinimumLength = 9)]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[@*#%]).+$",
            ErrorMessage = "Le mot de passe doit contenir au moins une majuscule, un chiffre, et un caractère spécial (@, *, #, %).")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirmer le nouveau mot de passe")]
        [Compare("NewPassword", ErrorMessage = "Le nouveau mot de passe et la confirmation ne correspondent pas.")]
        public string ConfirmPassword { get; set; }
    }
}