using Stat.Models;
using System.Collections.Generic;

namespace Stat.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        // For KPI Cards
        public int TotalSituations { get; set; }
        public int TotalDiwSituations { get; set; }
        public int TotalDriSituations { get; set; }
        public int TotalDcSituations { get; set; }
        public DashboardChartDataViewModel OperationalChartData { get; set; }
        public DashboardChartDataViewModel StrategicChartData { get; set; }

        public List<string> MissingDcSituationNames { get; set; }
        public List<DIW> MissingDiwSituations { get; set; }
        public List<MissingDriAlertViewModel> MissingDriSituations { get; set; }

        public string PeriodChecked { get; set; } 

        public int TotalUsers { get; set; }
        public int TotalDiwUsers { get; set; }
        public int TotalDriUsers { get; set; }
        public int TotalDcUsers { get; set; }

        public Dictionary<string, int> DiwCountByDri { get; set; }

        public AdminDashboardViewModel()
        {
            MissingDcSituationNames = new List<string>();
            MissingDiwSituations = new List<DIW>();
            MissingDriSituations = new List<MissingDriAlertViewModel>();
            DiwCountByDri = new Dictionary<string, int>();
        }
    }


    public class MissingDriAlertViewModel
    {
        public string DriName { get; set; }
        public List<string> MissingDiwNames { get; set; }
    }
}