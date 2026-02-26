// In Models/DashboardViewModels.cs

namespace Stat.Models
{
    // This model will hold the data for a single chart (for one category in one year)
    public class DashboardChartViewModel
    {
        public string ChartId { get; set; }
        public string CategoryName { get; set; }
        public List<string> Labels { get; set; } = new List<string>();

        public List<double> PerformanceJusquaCible { get; set; } = new List<double>();

        // The orange part: The gap remaining to reach the target.
        public List<double> EcartNegatif { get; set; } = new List<double>();

        // The blue part: The amount by which performance exceeded the target.
        public List<double> DepassementCible { get; set; } = new List<double>();

        // We still need these for the tooltip to show accurate totals.
        public List<double> CibleData { get; set; } = new List<double>();
        public List<double> TauxAtteintData { get; set; } = new List<double>();
    }

    // This model will hold all the summary information for a single year
    public class YearlySummaryViewModel
    {
        public int Year { get; set; }
        public int TotalSituations { get; set; }
        public int ConfirmedSituations { get; set; }
        public int PendingSituations { get; set; }
        public List<DashboardChartViewModel> Charts { get; set; } = new List<DashboardChartViewModel>();
    }

    // This will be the main model for our Index page
    public class DashboardViewModel
    {
        public int SituationsInProgress { get; set; } // Renamed from CurrentSituationsEnCours (Statut 0 or 2)
        public int SituationsPendingDRI { get; set; } // Renamed from CurrentSituationsConfirmes (Statut 1)
        public int SituationsValidated { get; set; }

        public int GrandTotalSituations { get; set; }
        public List<AlertViewModel> Alerts { get; set; } = new List<AlertViewModel>();

        public List<YearlySummaryViewModel> YearlySummaries { get; set; } = new List<YearlySummaryViewModel>();
    }
}