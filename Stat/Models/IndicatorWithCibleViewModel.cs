namespace Stat.Models
{
    // This is the ViewModel used by both the _Validite (draft) 
    // and Consulte (confirmed) pages.
    public class IndicatorWithCibleViewModel
    {
        public string IdIn { get; set; }
        public string IntituleIn { get; set; }
        public string IdCategIn { get; set; }
        public double CibleValue { get; set; }

        // Properties for the CONFIRMED view
        public float? Taux { get; set; }
        public float? Ecart { get; set; }

        // Properties for BOTH draft and confirmed views
        public double? Numerateur { get; set; }
        public double? Denominateur { get; set; }
    }
}