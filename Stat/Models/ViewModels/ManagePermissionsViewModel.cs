namespace Stat.Models.ViewModels
{
    public class ManagePermissionsViewModel
    {
        public string AdminId { get; set; }
        public string AdminName { get; set; }
        public List<PermissionAssignmentViewModel> AssignedPermissions { get; set; }
    }

    public class PermissionAssignmentViewModel
    {
        public string PermissionId { get; set; }
        public string Description { get; set; }
        public bool IsAssigned { get; set; }
    }
}