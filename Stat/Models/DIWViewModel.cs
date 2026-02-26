using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Stat.Models
{
    public class DIWViewModel
    {
        public string CodeDIW { get; set; }
        public string? LibelleDIW { get; set; }
        public string CodeDRI { get; set; }

       //public List<SelectListItem> DRIs { get; set; }
    }
}
