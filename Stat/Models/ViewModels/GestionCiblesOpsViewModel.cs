using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;

namespace Stat.Models.ViewModels
{
    public class GestionCiblesOpsViewModel
    {
        public List<SelectListItem> DiwOptions { get; set; }
        public string SelectedDiwCode { get; set; }
        // Defines the starting year
        public string SelectedYear { get; set; }
        public List<SelectListItem> YearOptions { get; set; }

        // This list is used for server-side binding if needed, 
        // though Ops uses JSON/JS primarily.
        public List<CibleOpViewModel> Cibles { get; set; }

        public GestionCiblesOpsViewModel()
        {
            YearOptions = new List<SelectListItem>();
            int currentYear = DateTime.Now.Year;
            // Offer a range of start years
            for (int i = currentYear; i <= currentYear + 2; i++)
            {
                YearOptions.Add(new SelectListItem { Value = i.ToString(), Text = i.ToString() });
            }
        }
    }

    
}