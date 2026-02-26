using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions; // ⚠️ Ajouter cet using si ce n'est pas déjà fait

namespace Stat.Models
{
    public class ResetPasswordViewModel
    {
        [Required]
        public string UserId { get; set; }

        // --- DÉBUT DES CONTRAINTES DE COMPLEXITÉ ---

        // La Regex vérifie:
        // (?=.*[A-Z]) : Doit contenir au moins une majuscule.
        // (?=.*[0-9]) : Doit contenir au moins un chiffre.
        // (?=.*[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]) : Doit contenir au moins un caractère spécial.
        // .{9,} : Doit contenir au moins 9 caractères.
        // Remarque : J'ai adapté la liste de caractères spéciaux pour qu'elle corresponde à celle de votre fichier ChangePassword.cshtml (avec quelques ajustements pour être compatible Regex).

        [Required(ErrorMessage = "Le nouveau mot de passe est requis")]
        [StringLength(100, ErrorMessage = "Le mot de passe doit comporter au moins 9 caractères.", MinimumLength = 9)]
        [DataType(DataType.Password)]
        [RegularExpression(
            @"^(?=.*[A-Z])(?=.*[0-9])(?=.*[~`!@#$%^&*()_+=\-\[\]\\';,./{}|"":<>?]).{9,}$",
            ErrorMessage = "Le mot de passe doit contenir 9+ caractères, une majuscule, un chiffre, et un caractère spécial."
        )]
        public string NewPassword { get; set; }
        // --- FIN DES CONTRAINTES DE COMPLEXITÉ ---

        [DataType(DataType.Password)]
        [Display(Name = "Confirmer le mot de passe")]
        [Compare("NewPassword", ErrorMessage = "Le mot de passe et le mot de passe de confirmation ne correspondent pas.")]
        public string ConfirmPassword { get; set; }
    }
}