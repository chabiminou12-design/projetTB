using System;
using System.Collections.Generic;

namespace Stat.Models.ViewModels
{
    public class DirectorUsersViewModel
    {
        public string StructureType { get; set; } // "DIW", "DRI", "DC"
        public string StructureName { get; set; }
        public List<UserGroupViewModel> Groups { get; set; } = new List<UserGroupViewModel>();
    }

    public class UserGroupViewModel
    {
        public string GroupName { get; set; } // e.g. "Agents DIW", "Staff DRI"
        public List<UserViewModel> Users { get; set; } = new List<UserViewModel>();
    }

 
}