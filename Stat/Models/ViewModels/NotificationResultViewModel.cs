// In Models/ViewModels/NotificationResultViewModel.cs
namespace Stat.Models.ViewModels
{
    public class NotificationResultViewModel
    {
        public List<AlertViewModel> Alerts { get; set; } = new List<AlertViewModel>();

        // This second list is only for DRIs
        public List<AlertViewModel> DiwSubmissionAlerts { get; set; } = new List<AlertViewModel>();
        public bool IsDRI { get; set; } = false;
    }
}