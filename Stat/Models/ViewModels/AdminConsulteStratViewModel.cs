using Stat.Models;
using System.Collections.Generic;

namespace Stat.Models.ViewModels
{
    // This is the main model for the view
    public class AdminConsulteStratViewModel
    {
        public Situation Situation { get; set; }
        public List<AxeViewModel> Axes { get; set; } = new List<AxeViewModel>();
    }

    // Represents a single Axe (Category)
    public class AxeViewModel
    {
        public string AxeName { get; set; }
        public List<ObjectifViewModel> Objectifs { get; set; } = new List<ObjectifViewModel>();
    }

    // Represents a single Objectif
    public class ObjectifViewModel
    {
        public string ObjectifName { get; set; }
        public List<DeclarationStrategique> Declarations { get; set; } = new List<DeclarationStrategique>();
    }
}