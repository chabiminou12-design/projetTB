using System.ComponentModel.DataAnnotations;

namespace Stat.Models
{
    public class AppSetting
    {
        [Key]
        public string Key { get; set; } // e.g., "SuperAdminUserId"

        public string Value { get; set; }
    }
}