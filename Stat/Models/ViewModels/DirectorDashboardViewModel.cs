namespace Stat.Models.ViewModels
{
    public class DirectorDashboardViewModel
    {
        public int TotalSituations { get; set; }
        public int SituationsInProgress { get; set; }
        public int SituationsPending { get; set; }
        public int SituationsValidated { get; set; }
    }
}