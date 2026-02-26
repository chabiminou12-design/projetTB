using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace Stat.Models.ViewModels
{
    public class NiveauOpViewModel
    {
        // Filter Values
        public string? SelectedYear { get; set; }
        public int? SelectedSemester { get; set; } // <-- ADD THIS
        public int? SelectedTrimester { get; set; }
        public int? SelectedMonth { get; set; }
        public string? SelectedDri { get; set; }
        public string? SelectedDiw { get; set; }
        public string? SelectedAxe { get; set; }
        public string? SelectedIndicateur { get; set; } // <-- ADD THIS

        // Options for Filter Dropdowns
        public List<SelectListItem> YearOptions { get; set; }
        public List<SelectListItem> DriOptions { get; set; }
        public List<SelectListItem> DiwOptions { get; set; }
        public List<SelectListItem> AxeOptions { get; set; }
        public List<SelectListItem> IndicateurOptions { get; set; } // <-- ADD THIS

        // A flag to know if we should display results
        public bool IsSearchPerformed { get; set; } // <-- ADD THIS

        // The final calculated results for the table
        public List<IndicatorPerformanceViewModel> PerformanceResults { get; set; }
    }
}