using Microsoft.AspNetCore.Mvc.Rendering;
using Stat.Models;
using System.Collections.Generic;

namespace Stat.Models.ViewModels
{
    public class AddIndicateurStratViewModel
    {
        public IndicateurStrategique Indicateur { get; set; }
        public List<SelectListItem> Categories { get; set; }
        public List<SelectListItem> Objectifs { get; set; }
        public float InitialCible { get; set; }
    }
}