using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Stat.Models
{
    public class ModeleOrgViewModel
    {
        public DRI dri {  get; set; }
        public DIW diw { get; set; }
        public DRIViewModel DRIVM { get; set; }
        public DIWViewModel DIWVM { get; set; }
        [Required]
        public string CodeDIW { get; set; }

        [Required]
        public string LibelleDIW { get; set; }

        [Required]
        public string SelectedDRI { get; set; }

        //public List<SelectListItem> DRIs { get; set; }
     
        public List<DRI> ListeDRIs { get; set; }

        
    }
}
