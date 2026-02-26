namespace Stat.Models.ViewModels
{
    public class SuperAdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalAdmins { get; set; }
        public int TotalDcs { get; set; }
        public int TotalDris { get; set; }
        public int TotalDiws { get; set; }
        public int TotalDirecteurs { get; set; }

        public int ActiveUserCount { get; set; }
        public int InactiveUserCount { get; set; }
        public int TotalSituationsSubmitted { get; set; }
        public int TotalSituationsValidated { get; set; }
        public double SystemCompletionRate { get; set; } // % of validated vs total

        public List<User> RecentlyCreatedUsers { get; set; } = new List<User>();
        public List<User> RecentlyConnectedUsers { get; set; } = new List<User>();
    }
}