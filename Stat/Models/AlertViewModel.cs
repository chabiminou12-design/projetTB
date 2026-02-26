// In Models/AlertViewModel.cs
namespace Stat.Models
{
    public class AlertViewModel
    {
        public string Message { get; set; }
        public string AlertType { get; set; } // "danger" for errors, "success" for good news
    }
}