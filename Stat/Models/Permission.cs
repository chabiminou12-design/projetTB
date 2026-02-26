using System.ComponentModel.DataAnnotations;

namespace Stat.Models
{
    public class Permission
    {
        [Key]
        public string PermissionId { get; set; } // e.g., "Permissions.Users.Create"

        [Required]
        public string Description { get; set; }
    }
}