using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace Stat.Models.ViewModels
{
    public class NiveauStratViewModel
    {
        // Filter Values
        public string? SelectedYear { get; set; }
        public int? SelectedSemester { get; set; } // <-- ADD THIS
        public int? SelectedTrimester { get; set; } // <-- ADD THIS
        public int? SelectedMonth { get; set; }     // <-- ADD THIS
        public string? SelectedAxe { get; set; }
        public int? SelectedObjectif { get; set; }

        // Options for Filter Dropdowns
        public List<SelectListItem> YearOptions { get; set; }
        public List<SelectListItem> AxeOptions { get; set; }
        public List<SelectListItem> ObjectifOptions { get; set; }

        public bool IsSearchPerformed { get; set; }
        public List<IndicatorStratPerformanceViewModel> PerformanceResults { get; set; }
    }
}