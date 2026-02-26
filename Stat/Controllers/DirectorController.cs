using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stat.Models;
using Stat.Models.Enums;
using Stat.Models.ViewModels;
using Stat.Helpers;
using System.Security.Claims;
using Microsoft.Extensions.Hosting;
using Stat.Services;
using System.Globalization;
using DocumentFormat.OpenXml.Bibliography;

namespace Stat.Controllers
{
    [Authorize(Policy = "DirectorAccess")]
    public class DirectorController : BaseController
    {
        private readonly DatabaseContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly IReportService _reportService;

        public DirectorController(DatabaseContext context, IWebHostEnvironment hostEnvironment, IReportService reportService)
            : base(context, hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
            _reportService = reportService;
        }

        // --- SMART DIRECTOR DASHBOARD ---
        // [Inside DirectorController.cs]

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var director = await _context.Users.FindAsync(userId);
            if (director == null) return RedirectToAction("Login", "Access");

            // 1. Base KPI Calculations
            var scopedQuery = await GetDirectorScopedSituationQuery();

            var viewModel = new DirectorDashboardViewModel
            {
                TotalSituations = await scopedQuery.CountAsync(),
                SituationsInProgress = await scopedQuery.CountAsync(s => s.Statut == 0 || s.Statut == 2),
                SituationsPending = await scopedQuery.CountAsync(s => s.Statut == 1),
                SituationsValidated = await scopedQuery.CountAsync(s => s.Statut == 3)
            };

            // 2. Identify Director Role
            var scope = await IdentifyDirectorScope(director.CodeDIW);
            ViewBag.DirectorType = scope.Type;
            ViewBag.StructureName = scope.Name;

            // 🌟 NOUVEAU LOGIQUE DRI POUR KPI SÉPARÉS 🌟
            if (scope.Type == "DRI")
            {
                var driCode = director.CodeDIW;

                // Situations propres au DRI
                var driScopedQuery = scopedQuery.Where(s => s.DIW == driCode);

                // Situations des DIW gérés
                var diwScopedQuery = scopedQuery.Where(s => s.DIW != driCode);

                ViewBag.DRI_TotalSituations = await driScopedQuery.CountAsync();
                ViewBag.DRI_SituationsInProgress = await driScopedQuery.CountAsync(s => s.Statut == 0 || s.Statut == 2);
                ViewBag.DRI_SituationsPending = await driScopedQuery.CountAsync(s => s.Statut == 1);
                ViewBag.DRI_SituationsValidated = await driScopedQuery.CountAsync(s => s.Statut == 3);

                ViewBag.DIW_TotalSituations = await diwScopedQuery.CountAsync();
                ViewBag.DIW_SituationsInProgress = await diwScopedQuery.CountAsync(s => s.Statut == 0 || s.Statut == 2);
                ViewBag.DIW_SituationsPending = await diwScopedQuery.CountAsync(s => s.Statut == 1);
                ViewBag.DIW_SituationsValidated = await diwScopedQuery.CountAsync(s => s.Statut == 3);
            }
            // 🌟 FIN NOUVEAU LOGIQUE DRI POUR KPI SÉPARÉS 🌟

            var yearlySummaries = new List<YearlySummaryViewModel>();

            // Pie Chart Data
            var statusPieData = new SituationStatusPieChartViewModel
            {
                Labels = new List<string> { "En Attente", "Validées", "Rejetées", "En Cours" },
                Data = new List<int> {
            viewModel.SituationsPending,
            viewModel.SituationsValidated,
            await scopedQuery.CountAsync(s => s.Statut == 2),
            await scopedQuery.CountAsync(s => s.Statut == 0)
        }
            };

            // ==========================================================================================
            // 3. FETCH DETAILED DATA BASED ON TYPE
            // ==========================================================================================

            if (scope.Type == "DC")
            {
                // --- DC LOGIC (Strategic Data - SNAPSHOT) ---
                var validatedSituations = await scopedQuery.Where(s => s.Statut == 3).ToListAsync();
                var latestSituationIds = validatedSituations
                    .GroupBy(s => s.Year)
                    .Select(g => g.OrderByDescending(s => GetMonthNumber(s.Month)).First().IDSituation)
                    .ToList();

                var declarations = await _context.DeclarationsStrategiques
                    .Include(d => d.Situation)
                    .Include(d => d.IndicateurStrategique).ThenInclude(i => i.CategorieIndicateur)
                    .Where(d => latestSituationIds.Contains(d.IDSituation))
                    .ToListAsync();

                var groupedByYear = declarations.GroupBy(d => d.Situation.Year).OrderByDescending(g => g.Key);

                foreach (var yearGroup in groupedByYear)
                {
                    if (!int.TryParse(yearGroup.Key, out int currentYear)) continue;
                    var summary = new YearlySummaryViewModel { Year = currentYear };

                    foreach (var catGroup in yearGroup.GroupBy(d => d.IndicateurStrategique.CategorieIndicateur))
                    {
                        var chart = CreateChartViewModel(currentYear, catGroup.Key.IdCategIn, catGroup.Key.IntituleCategIn);
                        foreach (var decl in catGroup.OrderBy(d => d.IndicateurStrategique.IdIndic))
                        {
                            AddToChart(chart, decl.IndicateurStrategique.IntituleIn, decl.taux, decl.Cible);
                        }
                        summary.Charts.Add(chart);
                    }
                    yearlySummaries.Add(summary);
                }
            }
            // ✨ LOGIQUE SPÉCIFIQUE POUR LES DRI ✨
            else if (scope.Type == "DRI")
            {
                var allYears = await scopedQuery.Select(s => s.Year).Distinct().ToListAsync();

                foreach (var yearStr in allYears.OrderByDescending(y => y))
                {
                    if (!int.TryParse(yearStr, out int currentYear)) continue;
                    var summary = new YearlySummaryViewModel { Year = currentYear };

                    // --- PART A: AGGREGATED DIW PERFORMANCE (Operational - SNAPSHOT) ---
                    // 1. Get the LATEST Situation ID for EACH managed DIW for this YEAR.
                    var managedSituations = await scopedQuery
                        .Where(s => s.Statut == 3 && s.Year == yearStr && s.DIW != director.CodeDIW)
                        .ToListAsync();

                    var latestSituationIds = managedSituations
                        .GroupBy(s => s.DIW) // Group by the DIW code
                        .Select(g => g.OrderByDescending(s => GetMonthNumber(s.Month)).First().IDSituation)
                        .ToList();

                    if (latestSituationIds.Any())
                    {
                        // 2. Fetch Declarations based ONLY on the LATEST Situation IDs
                        var diwDeclarations = await _context.Declarations
                            .Include(d => d.Indicateur).ThenInclude(i => i.CategorieIndicateur)
                            .Where(d => latestSituationIds.Contains(d.IDSituation))
                            .ToListAsync();

                        // 3. Group and Calculate Weighted Average (Moyenne Pondérée)
                        foreach (var catGroup in diwDeclarations.GroupBy(d => d.Indicateur.CategorieIndicateur))
                        {
                            var chart = CreateChartViewModel(currentYear, catGroup.Key.IdCategIn, $"Moyenne DIWs - {catGroup.Key.IntituleCategIn}");

                            foreach (var indicGroup in catGroup.GroupBy(d => d.Indicateur).OrderBy(g => g.Key.IdIn))
                            {
                                double totalNumerateur = indicGroup.Sum(x => x.Numerateur ?? 0);
                                double totalDenominateur = indicGroup.Sum(x => x.Denominateur ?? 0);

                                double avgTaux = (totalDenominateur > 0) ? (totalNumerateur / totalDenominateur) * 100 : 0;
                                double avgCible = indicGroup.Average(x => x.Cible ?? 0);

                                AddToChart(chart, indicGroup.Key.IntituleIn, avgTaux, avgCible);
                            }
                            summary.Charts.Add(chart);
                        }
                    }

                    // --- PART B: SPECIFIC DRI PERFORMANCE (Self-Reported - Indicators 5,6,7) ---
                    var driSelfSituation = await scopedQuery
                        .Where(s => s.Statut == 3 && s.Year == yearStr && s.DIW == director.CodeDIW)
                        .ToListAsync();

                    var latestSituation = driSelfSituation
                        .OrderByDescending(s => GetMonthNumber(s.Month))
                        .FirstOrDefault();

                    if (latestSituation != null)
                    {
                        var driDeclarations = await _context.DeclarationDRIs
                            .Include(d => d.Indicateur)
                            .Where(d => d.IDSituation == latestSituation.IDSituation)
                            .OrderBy(d => d.IdIndicacteur)
                            .ToListAsync();

                        if (driDeclarations.Any())
                        {
                            var driChart = new DashboardChartViewModel
                            {
                                CategoryName = "Performance Propre (Indicateurs DRI)",
                                ChartId = $"chart_{currentYear}_DRI_Specific"
                            };

                            foreach (var decl in driDeclarations)
                            {
                                string label = decl.Indicateur?.IntituleIn ?? $"Indicateur {decl.IdIndicacteur}";
                                AddToChart(driChart, label, decl.taux, decl.Cible);
                            }
                            summary.Charts.Add(driChart);
                        }
                    }

                    yearlySummaries.Add(summary);
                }
            }
            // LOGIQUE STANDARD DIW (pour un Directeur qui gère uniquement des DIW, sans rôle DRI)
            else
            {
                // 1. Get the LATEST Situation ID for EACH managed DIW for ALL years.
                var managedSituations = await scopedQuery
                    .Where(s => s.Statut == 3)
                    .ToListAsync();

                // Regrouper par DIW ET par Année, puis sélectionner le plus récent par mois.
                var latestSituationIds = managedSituations
                    .GroupBy(s => new { s.DIW, s.Year })
                    .Select(g => g.OrderByDescending(s => GetMonthNumber(s.Month)).First().IDSituation)
                    .ToList();

                // 2. Fetch Declarations based ONLY on the LATEST Situation IDs
                var declarations = await _context.Declarations
                    .Include(d => d.Situation)
                    .Include(d => d.Indicateur).ThenInclude(i => i.CategorieIndicateur)
                    .Where(d => latestSituationIds.Contains(d.IDSituation))
                    .ToListAsync();

                // 3. Group by Year and Category, and Calculate Weighted Average
                var groupedByYear = declarations.GroupBy(d => d.Situation.Year).OrderByDescending(g => g.Key);

                foreach (var yearGroup in groupedByYear)
                {
                    if (!int.TryParse(yearGroup.Key, out int currentYear)) continue;
                    var summary = new YearlySummaryViewModel { Year = currentYear };

                    foreach (var catGroup in yearGroup.GroupBy(d => d.Indicateur.CategorieIndicateur))
                    {
                        var chart = CreateChartViewModel(currentYear, catGroup.Key.IdCategIn, catGroup.Key.IntituleCategIn);
                        var indicGroups = catGroup.GroupBy(d => d.Indicateur);

                        foreach (var indicGroup in indicGroups.OrderBy(i => i.Key.IdIn))
                        {
                            double totalNumerateur = indicGroup.Sum(x => x.Numerateur ?? 0);
                            double totalDenominateur = indicGroup.Sum(x => x.Denominateur ?? 0);

                            double avgTaux = (totalDenominateur > 0) ? (totalNumerateur / totalDenominateur) * 100 : 0;
                            double avgCible = indicGroup.Average(x => x.Cible ?? 0);

                            AddToChart(chart, indicGroup.Key.IntituleIn, avgTaux, avgCible);
                        }
                        summary.Charts.Add(chart);
                    }
                    yearlySummaries.Add(summary);
                }
            }

            ViewBag.YearlySummaries = yearlySummaries;
            ViewBag.StatusPieChart = statusPieData;

            return View(viewModel);
        }
        // --- HELPER METHODS FOR CHARTS ---
        private DashboardChartViewModel CreateChartViewModel(int year, string catId, string catName)
        {
            return new DashboardChartViewModel
            {
                CategoryName = catName,
                ChartId = $"chart_{year}_{catId}"
            };
        }

        private void AddToChart(DashboardChartViewModel chart, string label, double? tauxVal, double? cibleVal)
        {
            chart.Labels.Add(label);
            double taux = tauxVal ?? 0;
            double cible = cibleVal ?? 0;

            chart.PerformanceJusquaCible.Add(Math.Min(taux, cible));
            chart.EcartNegatif.Add(Math.Max(0, cible - taux));
            chart.DepassementCible.Add(Math.Max(0, taux - cible));
            chart.TauxAtteintData.Add(taux);
            chart.CibleData.Add(cible);
        }

        private int GetMonthNumber(string monthName)
        {
            if (string.IsNullOrEmpty(monthName)) return 0;
            switch (monthName.ToLower())
            {
                case "janvier": return 1;
                case "février": return 2;
                case "mars": return 3;
                case "avril": return 4;
                case "mai": return 5;
                case "juin": return 6;
                case "juillet": return 7;
                case "août": return 8;
                case "septembre": return 9;
                case "octobre": return 10;
                case "novembre": return 11;
                case "décembre": return 12;
                default: return 0;
            }
        }


        // --- DIRECTOR'S LIST OF VALIDATED SITUATIONS (Unchanged) ---
        public async Task<IActionResult> ListValidatedSituations(int? pageNumber)
        {
            var scopedQuery = await GetDirectorScopedSituationQuery();

            var diws = await _context.DIWs.ToDictionaryAsync(d => d.CodeDIW, d => d.LibelleDIW);
            var dris = await _context.DRIs.ToDictionaryAsync(d => d.CodeDRI, d => d.LibelleDRI);
            var dcs = await _context.DCs.ToDictionaryAsync(d => d.CodeDC, d => d.LibelleDC);

            var situations = await scopedQuery
                .Where(s => s.Statut == 3)
                .OrderByDescending(s => s.DRIValidationDate ?? s.ConfirmDate)
                .ToListAsync();

            var viewModelData = situations.Select(s => {
                string structureName = "N/A";
                string situationType = "N/A";

                switch ((UserRole)s.User.Statut)
                {
                    case UserRole.DIW:
                        diws.TryGetValue(s.DIW, out structureName);
                        situationType = "Opérationnelle (DIW)";
                        break;
                    case UserRole.DRI:
                        dris.TryGetValue(s.DIW, out structureName);
                        situationType = "Opérationnelle (DRI)";
                        break;
                    case UserRole.DC:
                        dcs.TryGetValue(s.DIW, out structureName);
                        situationType = "Stratégique";
                        break;
                }

                return new ValidatedSituationViewModel
                {
                    SituationId = s.IDSituation,
                    Period = $"{s.Month} {s.Year}",
                    StructureName = structureName ?? s.DIW,
                    SituationType = situationType,
                    ValidationDate = s.DRIValidationDate ?? s.ConfirmDate
                };
            }).ToList();

            var paginatedModel = PaginatedList<ValidatedSituationViewModel>.Create(viewModelData, pageNumber ?? 1, 15);
            return View(paginatedModel);
        }

        // --- SECURE SITUATION CONSULTATION (Unchanged) ---
        [HttpGet]
        public async Task<IActionResult> ConsulteSituation(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var scopedQuery = await GetDirectorScopedSituationQuery();

            var situation = await scopedQuery
                .Include(s => s.DIWNavigation)
                .FirstOrDefaultAsync(s => s.IDSituation == id);

            if (situation == null) return Forbid();

            var user = await _context.Users.FindAsync(situation.User_id);
            if (user == null) return NotFound();

            ViewBag.Situation = situation;
            ViewBag.CurrentController = "Director";

            if (user.Statut == (int)UserRole.DC)
            {
                var dc = await _context.DCs.FindAsync(situation.DIW);
                ViewBag.StructureName = dc?.LibelleDC ?? "N/A";
                var declarations = await _context.DeclarationsStrategiques
                    .Where(d => d.IDSituation == id)
                    .Include(d => d.IndicateurStrategique.CategorieIndicateur)
                    .Include(d => d.IndicateurStrategique.Objectif)
                    .ToListAsync();
                var viewModel = new AdminConsulteStratViewModel { Situation = situation, Axes = declarations.GroupBy(d => d.IndicateurStrategique.CategorieIndicateur).Select(g => new AxeViewModel { AxeName = g.Key.IntituleCategIn, Objectifs = g.GroupBy(d2 => d2.IndicateurStrategique.Objectif).Select(g2 => new ObjectifViewModel { ObjectifName = g2.Key.Intituleobj, Declarations = g2.ToList() }).ToList() }).ToList() };
                return View("~/Views/Admin/ConsulteStrat.cshtml", viewModel);
            }
            else if (await _context.DeclarationDRIs.AnyAsync(d => d.IDSituation == id))
            {
                var dri = await _context.DRIs.FindAsync(situation.DIW);
                ViewBag.StructureName = dri?.LibelleDRI ?? "N/A";
                var declarations = await _context.DeclarationDRIs.Where(d => d.IDSituation == id).Include(d => d.Indicateur).ToListAsync();
                var viewModel = new AdminConsulteDriViewModel { Situation = situation, Declarations = declarations };
                return View("~/Views/Admin/ConsulteDRI.cshtml", viewModel);
            }
            else
            {
                ViewBag.StructureName = situation.DIWNavigation?.LibelleDIW ?? "N/A";
                var declarations = await _context.Declarations.Where(d => d.IDSituation == id).Include(d => d.Indicateur.CategorieIndicateur).ToListAsync();
                var viewModel = declarations.GroupBy(d => d.Indicateur.CategorieIndicateur).Select(g => new CategoryIndicatorGroup { CategoryName = g.Key.IntituleCategIn, Declarations = g.ToList() }).ToList();
                return View("~/Views/Admin/ConsulteOp.cshtml", viewModel);
            }
        }

        // --- CORE SECURITY: PRIVATE HELPER FOR DATA FILTERING (Unchanged) ---
        private async Task<IQueryable<Situation>> GetDirectorScopedSituationQuery()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var director = await _context.Users.FindAsync(userId);
            if (director == null) return Enumerable.Empty<Situation>().AsQueryable();

            var structureCode = director.CodeDIW;
            IQueryable<Situation> query = _context.Situations.Include(s => s.User);

            if (await _context.DRIs.AnyAsync(d => d.CodeDRI == structureCode)) // Is DRI Director?
            {
                var diwCodesForDri = await _context.DIWs
                    .Where(w => w.CodeDRI == structureCode)
                    .Select(w => w.CodeDIW)
                    .ToListAsync();
                return query.Where(s => s.DIW == structureCode || diwCodesForDri.Contains(s.DIW));
            }
            if (await _context.DCs.AnyAsync(d => d.CodeDC == structureCode)) // Is DC Director?
            {
                var userIdsForDc = await _context.Users
                    .Where(u => u.Statut == (int)UserRole.DC && u.CodeDIW == structureCode)
                    .Select(u => u.ID_User).ToListAsync();
                return query.Where(s => userIdsForDc.Contains(s.User_id));
            }
            return query.Where(s => s.DIW == structureCode);
        }

        // --- Helper to Identify Type (New for Dashboard) ---
        private async Task<(string Type, string Code, string Name)> IdentifyDirectorScope(string code)
        {
            var dc = await _context.DCs.FindAsync(code);
            if (dc != null) return ("DC", dc.CodeDC, dc.LibelleDC);

            var dri = await _context.DRIs.FindAsync(code);
            if (dri != null) return ("DRI", dri.CodeDRI, dri.LibelleDRI);

            var diw = await _context.DIWs.FindAsync(code);
            if (diw != null) return ("DIW", diw.CodeDIW, diw.LibelleDIW);

            return ("DIW", code, "Structure"); // Default to DIW behavior if unknown
        }

        public async Task<IActionResult> ExportToExcel(string id)
        {
            var scopedQuery = await GetDirectorScopedSituationQuery();
            if (!await scopedQuery.AnyAsync(s => s.IDSituation == id)) return Forbid();

            var situation = await _context.Situations.FindAsync(id);
            var user = await _context.Users.FindAsync(situation.User_id);

            if (user.Statut == (int)UserRole.DC)
            {
                var (fileContents, fileName) = await _reportService.GenerateStrategicExcelAsync(id);
                return File(fileContents, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            else if (await _context.DeclarationDRIs.AnyAsync(d => d.IDSituation == id))
            {
                var (fileContents, fileName) = await _reportService.GenerateDriExcelAsync(id);
                return File(fileContents, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            else
            {
                var (fileContents, fileName) = await _reportService.GenerateOperationalExcelAsync(id);
                return File(fileContents, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }

        public async Task<IActionResult> ExportToPdf(string id)
        {
            var scopedQuery = await GetDirectorScopedSituationQuery();
            if (!await scopedQuery.AnyAsync(s => s.IDSituation == id)) return Forbid();

            var situation = await _context.Situations.FindAsync(id);
            var user = await _context.Users.FindAsync(situation.User_id);

            if (user.Statut == (int)UserRole.DC)
            {
                var (fileContents, fileName) = await _reportService.GenerateStrategicPdfAsync(id);
                return File(fileContents, "application/pdf", fileName);
            }
            else if (await _context.DeclarationDRIs.AnyAsync(d => d.IDSituation == id))
            {
                var (fileContents, fileName) = await _reportService.GenerateDriPdfAsync(id);
                return File(fileContents, "application/pdf", fileName);
            }
            else
            {
                var (fileContents, fileName) = await _reportService.GenerateOperationalPdfAsync(id);
                return File(fileContents, "application/pdf", fileName);
            }
        }
        public async Task<IActionResult> ListUsers()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.ID_User == userId);
            var scope = await IdentifyDirectorScope(user.CodeDIW);

            var viewModel = new DirectorUsersViewModel { StructureType = scope.Type, StructureName = scope.Name };

            // Logic to fetch users based on hierarchy
            if ( scope.Type == "DIW")
            {
                viewModel.Groups.Add(new UserGroupViewModel
                {
                    GroupName = $"Personnel de la Direction de Wilaya (DIW: {scope.Name})",
                    Users = await GetUsersByStructure(scope.Code)
                });
            }
            else if(scope.Type == "DC" )
            {
                viewModel.Groups.Add(new UserGroupViewModel
                {
                    GroupName = $"Personnel de la Direction Centrale (DC: {scope.Name})",
                    Users = await GetUsersByStructure(scope.Code)
                });
            }
            else if (scope.Type == "DRI")
            {
                // Group 1: DRI Staff
                viewModel.Groups.Add(new UserGroupViewModel
                {
                    GroupName = $"Personnel de la Direction Régionale (DRI: {scope.Name})",
                    Users = await GetUsersByStructure(scope.Code)
                });

                // Group 2: Managed DIWs
                var childDiws = await _context.DIWs.AsNoTracking().Where(d => d.CodeDRI == scope.Code).ToListAsync();

                // MODIFICATION ICI: Parcourir TOUS les DIWs gérés, qu'ils aient des utilisateurs ou non
                foreach (var diw in childDiws)
                {
                    var diwUsers = await GetUsersByStructure(diw.CodeDIW);

                    // Supprimer la condition 'if (diwUsers.Any())' pour inclure les groupes vides

                    viewModel.Groups.Add(new UserGroupViewModel
                    {
                        GroupName = $"Personnel de la Direction de Wilaya (DIW: {diw.LibelleDIW})",
                        Users = diwUsers // Cette liste sera vide si aucun utilisateur n'est trouvé
                    });
                }
            }

            return View(viewModel);
        }

        private async Task<List<UserViewModel>> GetUsersByStructure(string structureCode)
        {

            string GetRole(string statut, int statutInt) => statut switch
            {
                "0" => $"Agent {((UserRole)statutInt).ToString()}",
                "1" => $"Agent validateur {((UserRole)statutInt).ToString()}",
                "2" => $"Agent {((UserRole)statutInt).ToString()}",
                "4" => $"DIRECTEUR",
                _ => "Unknown Role" // Handle other possible statuses
            };

            return await _context.Users
                .AsNoTracking()
                .Where(u => u.CodeDIW == structureCode)
                .Select(u => new UserViewModel
                {
                    UserId =u.ID_User ,
                    FullName = $"{u.FirstNmUser} {u.LastNmUser}",
                    Email = u.MailUser,
                    Phone = u.TelUser,
                    LastConnection = u.LastCnx,
                    Role = u.Statut.ToString() == "0" ? "Agent " + ((UserRole)0).ToString() :
                           u.Statut.ToString() == "1" ? "Agent validateur " + ((UserRole)1).ToString() :
                           u.Statut.ToString() == "2" ? "Agent " + ((UserRole)2).ToString() :
                           u.Statut.ToString() == "4" ? "DIRECTEUR" :
                           " "
                })
                .ToListAsync();
        }
    }
}