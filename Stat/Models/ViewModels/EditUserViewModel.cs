using Microsoft.AspNetCore.Mvc.Rendering;
using Stat.Models;
using System.Collections.Generic;

namespace Stat.Models.ViewModels
{
    public class EditUserViewModel
    {
        public User User { get; set; }

        // For the Statut/Role dropdown
        public List<SelectListItem> StatutOptions { get; set; }

        // For the dynamic DIW/DRI dropdowns
        public List<SelectListItem> DRIOptions { get; set; }
        public string SelectedDRI { get; set; }
    }
}