// In Models/ViewModels/AdminConsulteDriViewModel.cs
namespace Stat.Models.ViewModels
{
    public class AdminConsulteDriViewModel
    {
        public Situation Situation { get; set; }
        public List<DeclarationDRI> Declarations { get; set; }
    }
}