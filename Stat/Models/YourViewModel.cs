using Microsoft.AspNetCore.Mvc.Rendering;

namespace Stat.Models
{
    public class YourViewModel
    {
       public DIW? DIW { get; set; }
      // public IEnumerable<DRI>? DRIs { get; set; }
        public string CodeDC { get; set; }
        public string? LibelleDRI { get; set; }
        public string? LibelleDC { get; set; }
        public string CodeDIW { get; set; }
        public string LibelleDIW { get; set; }
        public string CodeDRI { get; set; }
        //public List<SelectListItem> DRIs { get; set; } // Pour la liste déroulante
        public DC? DCs { get; set; }
        public string SelectedDRI { get; set; }
        public string SelectedDIW { get; set; }

        public List<DRI> DRIs { get; set; }
    }
}
