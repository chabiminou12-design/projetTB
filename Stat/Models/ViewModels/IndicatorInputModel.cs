// In Models/ViewModels/IndicatorInputModel.cs
namespace Stat.Models.ViewModels
{
    public class IndicatorInputModel
    {
        public string IndicatorId { get; set; }
        public float? Numerateur { get; set; }
        public float? Denominateur { get; set; }
        public double cible { get; set; }
    }
}