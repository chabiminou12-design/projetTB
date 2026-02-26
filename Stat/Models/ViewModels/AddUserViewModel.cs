using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Stat.Models.ViewModels
{
    public class AddUserViewModel : IValidatableObject
    {
        public string UserType { get; set; }

        [Required(ErrorMessage = "Le prénom est requis.")]
        [Display(Name = "Prénom")]
        public string FirstNmUser { get; set; }

        [Required(ErrorMessage = "Le nom de famille est requis.")]
        [Display(Name = "Nom")]
        public string LastNmUser { get; set; }

        [Required(ErrorMessage = "L'email est requis.")]
        [EmailAddress(ErrorMessage = "Veuillez entrer une adresse email valide.")]
        [Display(Name = "Email")]
        public string MailUser { get; set; }

        [Required(ErrorMessage = "Le téléphone est requis.")]
        [Display(Name = "Téléphone")]
        public string TelUser { get; set; }

        [Required(ErrorMessage = "La date d'affectation est requise.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date d'Affectation")]
        public DateTime? Date_deb_Affect { get; set; }

        // Properties for structure selection dropdowns
        public string SelectedDRI { get; set; }
        public string SelectedDIW { get; set; }
        public IEnumerable<SelectListItem> DRIOptions { get; set; } = new List<SelectListItem>();
        public string SelectedDC { get; set; }
        public List<SelectListItem> DCOptions { get; set; }

        public List<PermissionAssignmentViewModel> PermissionsToAssign { get; set; } = new List<PermissionAssignmentViewModel>();

        // ===================================================================
        // ✨ ADD THESE PROPERTIES FOR THE DIRECTOR FORM ✨
        // ===================================================================

        // This holds the selected structure's code (e.g., a CodeDIW, CodeDRI, or CodeDC)
        // when creating a Director.
        [Display(Name = "Structure d'Affectation")]
        public string SelectedDirectorStructure { get; set; }

        // This holds the list of ALL DIWs to populate the dropdown when the user
        // selects "Directeur de DIW".
        public List<DIW> AllDiws { get; set; } = new List<DIW>();

        // ===================================================================


        // Intelligent validation logic for conditional requirements
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Rule 1: SelectedDRI is required ONLY for DRI and DIW users
            if (UserType == "DRI" || UserType == "DIW")
            {
                if (string.IsNullOrEmpty(SelectedDRI))
                {
                    yield return new ValidationResult("Veuillez sélectionner une Direction Régionale.", new[] { nameof(SelectedDRI) });
                }
            }

            // Rule 2: SelectedDIW is required ONLY for DIW users
            if (UserType == "DIW")
            {
                if (string.IsNullOrEmpty(SelectedDIW))
                {
                    yield return new ValidationResult("Veuillez sélectionner une Direction de Wilaya.", new[] { nameof(SelectedDIW) });
                }
            }

            // ✨ ADD THIS NEW RULE FOR DIRECTORS ✨
            // Rule 3: SelectedDirectorStructure is required ONLY for Director users
            if (UserType == "Director")
            {
                if (string.IsNullOrEmpty(SelectedDirectorStructure))
                {
                    yield return new ValidationResult("Veuillez sélectionner une structure d'affectation pour le directeur.", new[] { nameof(SelectedDirectorStructure) });
                }
            }
        }
    }
}