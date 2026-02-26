using Microsoft.AspNetCore.Mvc.Rendering;
using Stat.Models;
using System.Collections.Generic;

namespace Stat.Models.ViewModels
{
    public class AddIndicateurOpsViewModel
    {
        public Indicateur Indicateur { get; set; }
        public List<SelectListItem> Categories { get; set; }
    }
}