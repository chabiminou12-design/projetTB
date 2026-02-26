// In Models/ViewModels/DeleteDriViewModel.cs
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace Stat.Models.ViewModels
{
    public class DeleteDriViewModel
    {
        public DRI DriToDelete { get; set; }
        public List<DIW> DiwsToReassign { get; set; }
        public List<SelectListItem> OtherDris { get; set; }
        public Dictionary<string, string> DiwReassignments { get; set; } = new Dictionary<string, string>();
    }
}