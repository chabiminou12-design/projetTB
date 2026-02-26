// [Fichier: Models/ViewModels/EditIndicateurOpsViewModel.cs]
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace Stat.Models.ViewModels
{
    public class EditIndicateurOpsViewModel
    {
        public Indicateur Indicateur { get; set; }
        public List<SelectListItem> AxeOptions { get; set; }
    }
}