// Create this file at: Models/ViewModels/UserViewModel.cs
namespace Stat.Models.ViewModels
{
    public class UserViewModel
    {
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Role { get; set; }
        public DateTime? LastConnection { get; set; }
    }
}