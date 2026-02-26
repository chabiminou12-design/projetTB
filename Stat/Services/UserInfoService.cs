using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using Stat.Models;
using Stat.Models.Enums;
using System.Security.Claims;

namespace Stat.Services
{
    // This is a simple class to hold all the user info our layout needs
    public class LayoutUserData
    {
        public User CurrentUser { get; set; }
        public string RoleTitle { get; set; }
        public string StructureName { get; set; }
    }

    public class UserInfoService
    {
        private readonly DatabaseContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserInfoService(DatabaseContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<LayoutUserData> GetLayoutUserDataAsync()
        {
            var userId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return null; // No user logged in
            }

            var currentUser = await _context.Users.FindAsync(userId);
            if (currentUser == null)
            {
                return null; // User not found in DB
            }

            string structureName = " ";
            string roleTitle = " ";

            switch (currentUser.Statut)
            {
                case (int)UserRole.DIW:
                    var diw = await _context.DIWs.FindAsync(currentUser.CodeDIW);
                    structureName = diw?.LibelleDIW ?? "N/A";
                    roleTitle = $"Direction de Wilaya : {structureName}";
                    break;

                case (int)UserRole.DRI:
                    var dri = await _context.DRIs.FindAsync(currentUser.CodeDIW);
                    structureName = dri?.LibelleDRI ?? "N/A";
                    roleTitle = $"Direction Régionale : {structureName}";
                    break;

                case (int)UserRole.DC:
                    var dc = await _context.DCs.FindAsync(currentUser.CodeDIW);
                    structureName = dc?.LibelleDC ?? "N/A";
                    roleTitle = $"Direction Centrale : {structureName}";
                    break;

                case (int)UserRole.Admin:
                    structureName = "Accès Global";
                    roleTitle = $"Administration: {currentUser.User_name}";
                    break;

                case (int)UserRole.Director:
                    // Logic: A Director has a CodeDIW, but we don't know if it's a DC, DRI, or DIW ID.
                    // We check them one by one.

                    // 1. Check if it is a DC
                    var dcDir = await _context.DCs.FindAsync(currentUser.CodeDIW);
                    if (dcDir != null)
                    {
                        structureName = $"DC - {dcDir.LibelleDC}";
                    }
                    else
                    {
                        // 2. If not DC, Check if it is a DRI
                        var driDir = await _context.DRIs.FindAsync(currentUser.CodeDIW);
                        if (driDir != null)
                        {
                            structureName = $"DRI - {driDir.LibelleDRI}";
                        }
                        else
                        {
                            // 3. If not DRI, Check if it is a DIW
                            var diwDir = await _context.DIWs.FindAsync(currentUser.CodeDIW);
                            if (diwDir != null)
                            {
                                structureName = $"DIW - {diwDir.LibelleDIW}";
                            }
                            else
                            {
                                structureName = " ";
                            }
                        }
                    }
                    roleTitle = $"Directeur : {structureName}";
                    break;
            }

            return new LayoutUserData
            {
                CurrentUser = currentUser,
                RoleTitle = roleTitle,
                StructureName = structureName
            };
        }
    }
}