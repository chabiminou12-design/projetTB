// [Fichier: Models/ViewModels/EditIndicateurStratViewModel.cs]
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace Stat.Models.ViewModels
{
    public class EditIndicateurStratViewModel
    {
        public IndicateurStrategique Indicateur { get; set; }
        public List<SelectListItem> AxeOptions { get; set; }
        public List<SelectListItem> ObjectifOptions { get; set; }
    }
}