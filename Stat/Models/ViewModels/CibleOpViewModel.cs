namespace Stat.Models.ViewModels
{
    public class CibleOpViewModel
    {
        // Common Info
        public string CodeDIW { get; set; }
        public string IdIn { get; set; }
        public string IntituleIn { get; set; }
        public string AxeName { get; set; }

        // Year N (Start Year)
        public long id_cible1 { get; set; }
        public float cible1 { get; set; }

        // Year N+1
        public long id_cible2 { get; set; }
        public float cible2 { get; set; }

        // Year N+2
        public long id_cible3 { get; set; }
        public float cible3 { get; set; }
    }
}