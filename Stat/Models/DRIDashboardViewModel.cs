using Stat.Models.ViewModels;

namespace Stat.Models
{
    // New class to hold data for the DIW Performance stacked bar chart
    public class DiwPerformanceChartViewModel
    {
        public List<string> Labels { get; set; } = new List<string>(); // DIW Names
        public List<int> SituationsAtteintData { get; set; } = new List<int>(); // Blue part
        public List<int> EcartNegatifData { get; set; } = new List<int>(); // Orange part
        public int Target { get; set; } // The target (current month number)
    }

    // New class to hold data for the Status Pie Chart
    public class SituationStatusPieChartViewModel
    {
        public List<int> Data { get; set; } = new List<int>();
        public List<string> Labels { get; set; } = new List<string>();
    }
    public class ReviewSituationViewModel
    {
        public Situation Situation { get; set; }
        public List<CategoryIndicatorGroup> IndicatorGroups { get; set; } = new List<CategoryIndicatorGroup>();
    }

    public class CategoryIndicatorGroup
    {
        public string CategoryName { get; set; }
        public List<Declaration> Declarations { get; set; } = new List<Declaration>();
    }
    public class DRIDashboardViewModel
    {
        // Data for the KPI cards
        public int TotalDIWsManaged { get; set; }
        public int TotalSituationsSubmitted { get; set; }
        public int PendingDRIAproval { get; set; }
        public int RecentlyRejected { get; set; }

        // ✨ CORRECTION: Two distinct lists for the two types of alerts
        // For alerts about the DIWs this DRI manages (e.g., missing situations)
        public List<AlertViewModel> DiwSubmissionAlerts { get; set; }

        // For the DRI's own personal notifications (missing self-reports, admin validations)
        public List<AlertViewModel> DriPersonalNotifications { get; set; }

        // Data for the charts
        public DiwPerformanceChartViewModel DiwPerformanceChart { get; set; }
        public SituationStatusPieChartViewModel StatusPieChart { get; set; }

        // Data for other components
        public List<YearlySummaryViewModel> YearlySummaries { get; set; }
        public List<DiwComparisonViewModel> DiwComparisonData { get; set; }

        public DRIDashboardViewModel()
        {
            // Initialize all list and complex properties in the constructor
            DiwSubmissionAlerts = new List<AlertViewModel>();
            DriPersonalNotifications = new List<AlertViewModel>();
            YearlySummaries = new List<YearlySummaryViewModel>();
            DiwPerformanceChart = new DiwPerformanceChartViewModel();
            StatusPieChart = new SituationStatusPieChartViewModel();
            DiwComparisonData = new List<DiwComparisonViewModel>();
        }
    }
}