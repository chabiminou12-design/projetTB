// Create this file at: Models/ViewModels/DIWUsersViewModel.cs
namespace Stat.Models.ViewModels
{
    public class DIWUsersViewModel
    {
        public string CodeDIW { get; set; }
        public string LibelleDIW { get; set; }
        public List<UserViewModel> Users { get; set; }
    }
}