using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace Stat.Models.ViewModels
{
    public class GestionCiblesDriViewModel
    {
        public string SelectedDriCode { get; set; }
        public string SelectedYear { get; set; } // Start Year

        public List<SelectListItem> DriOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> YearOptions { get; set; } = new List<SelectListItem>();

        public List<CibleDriRowViewModel> CiblesRows { get; set; } = new List<CibleDriRowViewModel>();
    }

    public class CibleDriRowViewModel
    {
        public int IdIndicacteur { get; set; }
        public string IntituleIndicateur { get; set; }

        // Year N
        public int IdCible1 { get; set; }
        public float ValCible1 { get; set; }

        // Year N+1
        public int IdCible2 { get; set; }
        public float ValCible2 { get; set; }

        // Year N+2
        public int IdCible3 { get; set; }
        public float ValCible3 { get; set; }
    }
}