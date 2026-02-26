namespace Stat.Models.ViewModels
{
    // This ViewModel represents a single row in the strategic indicators list.
    public class ListIndicateurStratViewModel
    {
        public string IdIndic { get; set; }
        public string AxeName { get; set; }
        public string ObjectifName { get; set; }
        public string IntituleIn { get; set; }

        // This property will hold the target for the current year.
        public float? CibleAnneeEnCours { get; set; }
    }
}