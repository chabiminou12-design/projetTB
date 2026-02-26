using Stat.Models;
using System.Collections.Generic;

namespace Stat.Models.ViewModels
{
    public class ProfileViewModel
    {
        // User Details
        public User UserProfile { get; set; }
        public string RoleName { get; set; }
        public string StructureName { get; set; }
        public string ParentStructureName { get; set; }

        // KPI Stats
        public int TotalSituations { get; set; }
        public int PendingSituations { get; set; } // Pending DRI validation
        public int ValidatedSituations { get; set; }
        public int RejectedSituations { get; set; }

        // Recent Activity
        public List<Situation> RecentSituations { get; set; }

        public ProfileViewModel()
        {
            RecentSituations = new List<Situation>();
        }
    }
}