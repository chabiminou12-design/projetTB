namespace Stat.Models.ViewModels
{
    public class DiwComparisonViewModel
    {
        public string DiwName { get; set; }
        public double OverallPerformance { get; set; }
        public double TotalSituationsCount { get; set; }
        public double ValideSituationsCount { get; set; }
        public double ManqueSituationsCount { get; set; }
        
        public int PendingSituationsCount { get; set; }
        public int RejectedSituationsCount { get; set; }
    }
}