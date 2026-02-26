namespace Stat.Models
{
    public class UserPermission
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string PermissionId { get; set; }

        // Navigation properties
        public virtual User User { get; set; }
        public virtual Permission Permission { get; set; }
    }
}