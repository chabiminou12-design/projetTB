using System.Collections.Generic;

namespace Stat.Models.ViewModels
{
    public class DashboardChartDataViewModel
    {
        public List<string> Labels { get; set; } = new List<string>();
        public List<double> Data { get; set; } = new List<double>();
    }
}