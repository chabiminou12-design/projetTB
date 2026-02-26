using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace Stat.Models.ViewModels
{
    // ViewModel for the main page, holding filter options and results
    public class NiveauOpDRIViewModel
    {
        public List<SelectListItem> YearOptions { get; set; }
        public List<SelectListItem> AxeOptions { get; set; }
        public List<SelectListItem> IndicateurOptions { get; set; }
        public List<IndicatorPerformanceDRIViewModel> PerformanceResults { get; set; }

        // Selected filter values
        public string SelectedYear { get; set; }
        public int? SelectedSemester { get; set; }
        public int? SelectedTrimester { get; set; }
        public int? SelectedMonth { get; set; }
        public string SelectedAxe { get; set; }
        public string SelectedIndicateur { get; set; }

        // To know when to show results vs. the initial message
        public bool IsSearchPerformed { get; set; }

        public NiveauOpDRIViewModel()
        {
            PerformanceResults = new List<IndicatorPerformanceDRIViewModel>();
        }
    }

    // ViewModel for each row in the results table
    public class IndicatorPerformanceDRIViewModel
    {
        public string AxeName { get; set; }
        public string IndicatorName { get; set; }
        public double SumNumerateur { get; set; }
        public double SumDenominateur { get; set; }
        public double Taux { get; set; }

    }
}