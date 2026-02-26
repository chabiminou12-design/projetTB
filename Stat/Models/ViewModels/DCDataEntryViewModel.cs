// Dans Models/ViewModels/DCDataEntryViewModel.cs
namespace Stat.Models.ViewModels
{
    public class DCDataEntryViewModel
    {
        public List<CategoryViewModel> Categories { get; set; } = new List<CategoryViewModel>();
    }

    public class CategoryViewModel
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public List<ObjectiveViewModel> Objectives { get; set; } = new List<ObjectiveViewModel>();
    }

    public class ObjectiveViewModel
    {
        public string ObjectiveName { get; set; }
        public List<IndicatorViewModel> Indicators { get; set; } = new List<IndicatorViewModel>();
    }

    public class IndicatorViewModel
    {
        public string IdIndic { get; set; }
        public string IntituleIn { get; set; }
        public double Cible { get; set; }
    }
}