namespace Stat.Models.ViewModels
{
    public class IndicatorPerformanceViewModel
    {
        public string AxeName { get; set; }
        public string IndicatorName { get; set; }
        public double SumNumerateur { get; set; } 
        public double SumDenominateur { get; set; }
        public double Taux { get; set; }
        public double Cible { get; set; }
        public double Ecart { get; set; }
    }
}