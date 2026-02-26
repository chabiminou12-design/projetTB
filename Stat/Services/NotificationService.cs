using Microsoft.EntityFrameworkCore;
using Stat.Models;
using Stat.Models.Enums;
using Stat.Models.ViewModels;
using System.Globalization;
using System.Security.Claims;

namespace Stat.Services
{
    public class NotificationService : INotificationService
    {
        private readonly DatabaseContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public NotificationService(DatabaseContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<NotificationResultViewModel> GetNotificationsAsync()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null || !user.Identity.IsAuthenticated)
            {
                return new NotificationResultViewModel();
            }

            var result = new NotificationResultViewModel();
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

            // ✨ 1. Get the Structure Code (CodeDIW) from claims or DB
            // This is crucial to link users of the same structure
            var userStructureCode = user.FindFirstValue("CodeDIW");

            var userRoleString = user.FindFirstValue(ClaimTypes.Role);
            if (string.IsNullOrEmpty(userRoleString) || !Enum.TryParse(typeof(UserRole), userRoleString, out var userRoleObject))
            {
                return result;
            }
            var userRole = (UserRole)userRoleObject;
            var today = DateTime.Now;
            var isCurrentUserSuperAdmin = user.HasClaim("IsSuperAdmin", "true");
            //*****************************************rapport
            if (userRole == UserRole.DIW || userRole == UserRole.DRI)
            {
                string rapportType = "";
                string currentYear = today.Year.ToString();

                // ** TESTING MODE: December (12) acts as July (7) **
                if (today.Month == 7 || today.Month == 8)
                {
                    rapportType = "Trimestriel";
                }
                else if (today.Month == 1 || today.Month == 2)
                {
                    rapportType = "Annuel";
                    currentYear = (today.Year - 1).ToString();
                }

                if (!string.IsNullOrEmpty(rapportType))
                {
                    // Fetch the LATEST report for this period
                    var report = await _context.Rapports
                        .Where(r => r.CodeStructure == userStructureCode && r.Type == rapportType && r.Year == currentYear)
                        .OrderByDescending(r => r.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (report == null)
                    {
                        // Condition: REQUIRED MONTH + NO REPORT
                        result.Alerts.Add(new AlertViewModel
                        {
                            Message = $"Action requise : Veuillez envoyer le Rapport {rapportType} ({currentYear}).",
                            AlertType = "warning"
                        });
                    }
                    else if (report.Status == 2)
                    {
                        // Condition: REPORT REJECTED
                        result.Alerts.Add(new AlertViewModel
                        {
                            Message = $"Rapport {rapportType} REJETÉ. Motif : {report.Motif}. Veuillez renvoyer une version corrigée.",
                            AlertType = "danger"
                        });
                    }
                    else if (report.Status == 1)
                    {
                        // Condition: REPORT VALIDATED (Show for 2 days only)
                        if (report.CreatedAt > today.AddDays(-2) || (report.Id > 0)) // Check validation date if you have one, or creation date of the valid record
                        {
                            result.Alerts.Add(new AlertViewModel
                            {
                                Message = $"Votre Rapport {rapportType} a été validé avec succès.",
                                AlertType = "success"
                            });
                        }
                    }
                }
            }
            //*****************************************rapport
            // This logic ONLY runs for submission roles (DIW, DRI, DC).
            if (userRole == UserRole.DIW || userRole == UserRole.DRI || userRole == UserRole.DC)
            {
                
                var dbUser = await _context.Users.FindAsync(userId);
                if (dbUser != null)
                {
                    // ✨ 2. PERSONAL SITUATIONS (For Success/Rejection Alerts)
                    // We keep this filter by 'User_id' because validation alerts must be personal
                    var mySituations = await _context.Situations.Where(s => s.User_id == userId).ToListAsync();

                    var recentlyValidated = mySituations.Where(s => s.Statut == 3 && (s.DRIValidationDate > today.AddDays(-3) || s.AdminValidationDate > today.AddDays(-3))).ToList();
                    foreach (var situation in recentlyValidated)
                    {
                        string validator = situation.AdminValidationDate.HasValue ? "l'administrateur" : "le DRI";
                        result.Alerts.Add(new AlertViewModel { Message = $"Félicitations ! Votre situation pour {situation.Month} {situation.Year} a été validée par {validator}.", AlertType = "success" });
                    }
                    var rejected = mySituations.Where(s => s.Statut == 2).ToList();
                    if (rejected.Any()) { result.Alerts.Add(new AlertViewModel { Message = $"Vous avez {rejected.Count} situation(s) rejetée(s) qui nécessitent votre attention.", AlertType = "danger" }); }

                    // =========================================================
                    // ✨ FIX: STRUCTURE MISSING SITUATIONS (Shared among colleagues)
                    // =========================================================

                    // 1. Fetch situations for the WHOLE STRUCTURE (CodeDIW), not just the user.
                    // This ensures r.fafi sees work done by c.aymen.
                    var structureSituations = await _context.Situations
                        .Where(s => s.DIW == dbUser.CodeDIW)
                        .ToListAsync();

                    DateTime deploymentDate = new DateTime(2025, 6, 1);
                    DateTime userJoinDate = dbUser.Date_deb_Affect ?? today;
                    DateTime effectiveStartDate = (userJoinDate > deploymentDate) ? userJoinDate : deploymentDate;

                    var lastMonthToCheck = today.AddMonths(-1);

                    // ✨ Use 'structureSituations' for the lookup, not 'mySituations'
                    var situationsLookup = structureSituations.ToLookup(s => $"{s.Month.ToLower()}-{s.Year}");

                    var currentDate = new DateTime(effectiveStartDate.Year, effectiveStartDate.Month, 1);

                    while (currentDate <= lastMonthToCheck)
                    {
                        string monthName = currentDate.ToString("MMMM", new CultureInfo("fr-FR"));
                        string year = currentDate.Year.ToString();

                        if (!situationsLookup.Contains($"{monthName.ToLower()}-{year}"))
                        {
                            result.Alerts.Add(new AlertViewModel
                            {
                                Message = $"En retard: La situation de {monthName} {year} est MANQUANTE.",
                                AlertType = "danger"
                            });
                        }
                        currentDate = currentDate.AddMonths(1);
                    }
                    // =========================================================
                }
            }

            // Logic for Admins and Super Admins (Unchanged)
            if (userRole == UserRole.Admin)
            {
                if (!isCurrentUserSuperAdmin)
                {
                    var adminValidatableUserIds = await _context.Users.Where(u => u.Statut == (int)UserRole.DRI || u.Statut == (int)UserRole.DC).Select(u => u.ID_User).ToListAsync();
                    var pendingSituations = await _context.Situations.Where(s => s.Statut == 1 && adminValidatableUserIds.Contains(s.User_id)).Include(s => s.User).ToListAsync();
                    if (pendingSituations.Any())
                    {
                        var dris = await _context.DRIs.ToDictionaryAsync(d => d.CodeDRI, d => d.LibelleDRI);
                        var dcs = await _context.DCs.ToDictionaryAsync(d => d.CodeDC, d => d.LibelleDC);
                        foreach (var situation in pendingSituations)
                        {
                            string structureType = "Structure"; string structureName = "Inconnue";
                            if (situation.User.Statut == (int)UserRole.DRI) { structureType = "La DRI"; dris.TryGetValue(situation.DIW, out structureName); }
                            else if (situation.User.Statut == (int)UserRole.DC) { structureType = "La DC"; dcs.TryGetValue(situation.DIW, out structureName); }
                            result.Alerts.Add(new AlertViewModel { Message = $"{structureType} '{structureName}' a soumis la situation de {situation.Month} {situation.Year} pour validation.", AlertType = "warning" });
                        }
                    }
                }

                if (isCurrentUserSuperAdmin)
                {
                    var newlyCreatedUsers = await _context.Users
                        .Where(u => u.DateDeCreation > today.AddDays(-2) && u.CreatedByUserId != null)
                        .ToListAsync();

                    if (newlyCreatedUsers.Any())
                    {
                        var creatorIds = newlyCreatedUsers.Select(u => u.CreatedByUserId).Distinct().ToList();
                        var creators = await _context.Users
                            .Where(u => creatorIds.Contains(u.ID_User))
                            .ToDictionaryAsync(u => u.ID_User);

                        foreach (var newUser in newlyCreatedUsers)
                        {
                            creators.TryGetValue(newUser.CreatedByUserId, out var creator);
                            string creatorName = creator?.User_name ?? "un administrateur";
                            string roleName = ((UserRole)newUser.Statut).ToString();
                            result.Alerts.Add(new AlertViewModel
                            {
                                Message = $"Un nouvel utilisateur ({roleName}) a été créé par {creatorName}.",
                                AlertType = "info"
                            });
                        }
                    }
                }
            }

            // DRI-specific logic for missing DIW submissions (Unchanged)
            if (userRole == UserRole.DRI)
            {
                var userCodeDiw = user.FindFirstValue("CodeDIW");
                result.IsDRI = true;
                var managedDiws = await _context.DIWs.Where(d => d.CodeDRI == userCodeDiw).ToListAsync();
                var managedDiwCodes = managedDiws.Select(d => d.CodeDIW).ToList();
                var allManagedSituations = await _context.Situations.Where(s => managedDiwCodes.Contains(s.DIW)).ToListAsync();
                var lastMonthToCheck = today.AddMonths(-1);

                DateTime deploymentDate = new DateTime(2025, 6, 1);
                DateTime standardLookback = new DateTime(today.Year - 1, 1, 1);
                DateTime effectiveLoopStart = (standardLookback > deploymentDate) ? standardLookback : deploymentDate;

                foreach (var diw in managedDiws)
                {
                    var loopDate = effectiveLoopStart;

                    while (loopDate <= lastMonthToCheck)
                    {
                        string monthName = loopDate.ToString("MMMM", new CultureInfo("fr-FR"));
                        string year = loopDate.Year.ToString();

                        if (!allManagedSituations.Any(s => s.DIW == diw.CodeDIW && s.Month.Equals(monthName, StringComparison.OrdinalIgnoreCase) && s.Year == year))
                        {
                            result.DiwSubmissionAlerts.Add(new AlertViewModel
                            {
                                Message = $"Alerte: La situation de {monthName} {year} pour le DIW '{diw.LibelleDIW}' est manquante.",
                                AlertType = "danger"
                            });
                        }
                        loopDate = loopDate.AddMonths(1);
                    }
                }
            }

            return result;
        }
    }
}