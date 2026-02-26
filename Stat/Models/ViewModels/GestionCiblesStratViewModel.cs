using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace Stat.Models.ViewModels
{
    public class GestionCiblesStratViewModel
    {
        public string SelectedYear { get; set; } // Start Year
        public List<SelectListItem> YearOptions { get; set; }
        public List<CibleStratViewModel> Cibles { get; set; }

        public GestionCiblesStratViewModel()
        {
            YearOptions = new List<SelectListItem>();
            int currentYear = System.DateTime.Now.Year;
            for (int i = currentYear; i <= currentYear + 2; i++)
            {
                YearOptions.Add(new SelectListItem { Value = i.ToString(), Text = i.ToString() });
            }
        }
    }

    public class CibleStratViewModel
    {
        public string IdIndic { get; set; }
        public string AxeName { get; set; }
        public string ObjectifName { get; set; }
        public string IntituleIn { get; set; }

        // Year N
        public long IdCible1 { get; set; }
        public float Cible1 { get; set; }

        // Year N+1
        public long IdCible2 { get; set; }
        public float Cible2 { get; set; }

        // Year N+2
        public long IdCible3 { get; set; }
        public float Cible3 { get; set; }
    }
}