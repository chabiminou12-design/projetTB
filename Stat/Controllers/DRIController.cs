using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Stat.Models;
using System.Security.Claims;
using System.Threading.Tasks;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using Stat.Models.ViewModels;
using Stat.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Globalization;
using Stat.Models.Enums;
namespace Stat.Controllers
{
    [Authorize(Policy = "DRIAccess")]
    public class DRIController : BaseController
    {
        private readonly DatabaseContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly IReportService _reportService;

        public DRIController(DatabaseContext context, IWebHostEnvironment hostEnvironment, IReportService reportService)
            : base(context, hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
            _reportService = reportService;
        }

        // In Controllers/DRIController.cs

       // Locate the Index() method and replace it with this updated version
        public async Task<IActionResult> Index()
        {
            var driCode = User.FindFirstValue("CodeDIW");
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var viewModel = new DRIDashboardViewModel();
            var today = DateTime.Now;
            //***********************************************************debut rapport

            string rapportType = "";
            string rapportYear = today.Year.ToString();

            if (today.Month == 7 || today.Month == 8)
            {
                rapportType = "Trimestriel";
            }
            else if (today.Month == 1 || today.Month == 2)
            {
                rapportType = "Annuel";
                rapportYear = (today.Year - 1).ToString();
            }

            bool showUpload = false;

            if (!string.IsNullOrEmpty(rapportType))
            {
                var latestReport = await _context.Rapports
                    .Where(r => r.CodeStructure == driCode && r.Type == rapportType && r.Year == rapportYear)
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefaultAsync();

                // SHOW IF: (No Report Found) OR (Report is Rejected/Status 2)
                if (latestReport == null || latestReport.Status == 2)
                {
                    showUpload = true;
                }
            }

            ViewBag.ShowRapportUpload = showUpload;
            //***********************************************************fin rpport 
            // --- 1. Get Base Data ---
            // A. Managed DIWs (Consolidated Data)
            var managedDiws = await _context.DIWs.Where(d => d.CodeDRI == driCode).ToListAsync();
            var managedDiwCodes = managedDiws.Select(d => d.CodeDIW).ToList();
            var allManagedSituations = await _context.Situations
                .Where(s => managedDiwCodes.Contains(s.DIW))
                .Include(s => s.DIWNavigation)
                .ToListAsync();

            // B. DRI Self Data (Performance Propre)
            var mySituations = await _context.Situations
                .Where(s => s.User_id == currentUserId)
                .ToListAsync();

            // --- 2. Calculate KPI Cards (Split View) ---
    
            // Set 1: Performance Propre (DRI Self) - Using ViewBag to match Director style
            ViewBag.DRI_TotalSituations = mySituations.Count();
            ViewBag.DRI_SituationsInProgress = mySituations.Count(s => s.Statut == 0 || s.Statut == 2);
            ViewBag.DRI_SituationsPending = mySituations.Count(s => s.Statut == 1);
            ViewBag.DRI_SituationsValidated = mySituations.Count(s => s.Statut == 3);

            // Set 2: Performance Consolidée (DIW Managed)
            ViewBag.DIW_TotalSituations = allManagedSituations.Count();
            ViewBag.DIW_SituationsInProgress = allManagedSituations.Count(s => s.Statut == 0 || s.Statut == 2);
            ViewBag.DIW_SituationsPending = allManagedSituations.Count(s => s.Statut == 1);
            ViewBag.DIW_SituationsValidated = allManagedSituations.Count(s => s.Statut == 3);

            // Keep existing Model properties for backward compatibility if needed, or update them
            viewModel.TotalDIWsManaged = managedDiws.Count();
            viewModel.TotalSituationsSubmitted = allManagedSituations.Count();
            viewModel.PendingDRIAproval = allManagedSituations.Count(s => s.Statut == 1);
            viewModel.RecentlyRejected = allManagedSituations.Count(s => s.Statut == 2 && s.EditDate > today.AddDays(-30));

            // --- 3. Logic for DIW Performance Stacked Bar Chart (Unchanged) ---
            var performanceChart = new DiwPerformanceChartViewModel();
            int targetMonth;

            // 1. Check if the current month is January (Month 1)
            if (today.Month == 1)
            {
                // If it's January, set the target to 12 (representing the target for the whole past year, or 12 periods)
                targetMonth = 12;
            }
            else
            {
                // For all other months (Feb through Dec), the target is the number of completed months (Current Month - 1)
                targetMonth = today.Month - 1;
            }
            performanceChart.Target = targetMonth;
            var situationsThisYear = allManagedSituations.Where(s => s.Year == today.Year.ToString()).ToList();

            foreach (var diw in managedDiws.OrderBy(d => d.LibelleDIW))
            {
                performanceChart.Labels.Add(diw.LibelleDIW);
                int count = situationsThisYear.Count(s => s.DIW == diw.CodeDIW);
                performanceChart.SituationsAtteintData.Add(Math.Min(count, targetMonth));
                performanceChart.EcartNegatifData.Add(Math.Max(0, targetMonth - count));
            }
            viewModel.DiwPerformanceChart = performanceChart;

            // --- 4. Logic for Status Distribution Pie Chart (Combined) ---
            var pieChart = new SituationStatusPieChartViewModel();
            pieChart.Labels = new List<string> { "En Attente", "Validées", "Rejetées", "En Cours" };
            // Combining counts for a global view in the Pie Chart
            pieChart.Data = new List<int>
            {
                allManagedSituations.Count(s => s.Statut == 1) + mySituations.Count(s => s.Statut == 1),
                allManagedSituations.Count(s => s.Statut == 3) + mySituations.Count(s => s.Statut == 3),
                allManagedSituations.Count(s => s.Statut == 2) + mySituations.Count(s => s.Statut == 2),
                allManagedSituations.Count(s => s.Statut == 0) + mySituations.Count(s => s.Statut == 0)
            };
            viewModel.StatusPieChart = pieChart;

            // --- 5. Logic for Yearly Performance Charts (Consolidated + DRI Specific) ---
    
            // Get all relevant years from both datasets
            var allYears = allManagedSituations.Select(s => s.Year)
                .Union(mySituations.Select(s => s.Year))
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();

            foreach (var yearStr in allYears)
            {
                if (!int.TryParse(yearStr, out int currentYear)) continue;
                var yearSummary = new YearlySummaryViewModel { Year = currentYear };

                // A. CONSOLIDATED DIW CHARTS (Existing Logic)
                var latestSituationIds = allManagedSituations
                    .Where(s => s.Statut == 3 && s.Year == yearStr)
                    .GroupBy(s => s.DIW)
                    .Select(g => g.OrderByDescending(s => GetMonthNumber(s.Month)).First().IDSituation)
                    .ToList();

                if (latestSituationIds.Any())
                {
                    var relevantDeclarations = await _context.Declarations
                        .Include(d => d.Indicateur).ThenInclude(i => i.CategorieIndicateur)
                        .Where(d => latestSituationIds.Contains(d.IDSituation))
                        .ToListAsync();

                    var declarationsByCategory = relevantDeclarations.GroupBy(d => d.Indicateur.CategorieIndicateur);
                    foreach (var categoryGroup in declarationsByCategory)
                    {
                        var chart = new DashboardChartViewModel
                        {
                            CategoryName = $"Moyenne DIWs - {categoryGroup.Key.IntituleCategIn}", // Renamed for clarity
                            ChartId = $"chart_{currentYear}_{categoryGroup.Key.IdCategIn}"
                        };

                        var indicatorsInCategory = categoryGroup.GroupBy(d => d.Indicateur);
                        foreach (var indicatorGroup in indicatorsInCategory.OrderBy(ig => ig.Key.IdIn))
                        {
                            chart.Labels.Add(indicatorGroup.Key.IntituleIn);

                            double totalNumerateur = indicatorGroup.Sum(d => d.Numerateur ?? 0);
                            double totalDenominateur = indicatorGroup.Sum(d => d.Denominateur ?? 0);
                            double cible = indicatorGroup.Average(d => d.Cible ?? 0.0);
                            double taux = (totalDenominateur != 0) ? (totalNumerateur / totalDenominateur) * 100 : 0;
                    
                            chart.PerformanceJusquaCible.Add(Math.Min(taux, cible));
                            chart.EcartNegatif.Add(Math.Max(0, cible - taux));
                            chart.DepassementCible.Add(Math.Max(0, taux - cible));
                            chart.TauxAtteintData.Add(taux);
                            chart.CibleData.Add(cible);
                        }
                        yearSummary.Charts.Add(chart);
                    }
                }

                // B. DRI PERFORMANCE PROPRE CHART (New Feature)
                var latestDriSituation = mySituations
                    .Where(s => s.Statut == 3 && s.Year == yearStr)
                    .OrderByDescending(s => GetMonthNumber(s.Month))
                    .FirstOrDefault();

                if (latestDriSituation != null)
                {
                    var driDeclarations = await _context.DeclarationDRIs
                        .Include(d => d.Indicateur)
                        .Where(d => d.IDSituation == latestDriSituation.IDSituation)
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
                                    // 1. Convert nullable types to standard doubles, handling nulls with 0
                                    double tauxVal = (double)(decl.taux ?? 0);
                                    double cibleVal = (double)decl.Cible;

                                    // 2. Add to lists using the converted values
                                    driChart.Labels.Add(decl.Indicateur?.IntituleIn ?? $"Indicateur {decl.IdIndicacteur}");

                                    // FIX: We use the variables created above (tauxVal, cibleVal)
                                    driChart.PerformanceJusquaCible.Add(Math.Min(tauxVal, cibleVal));
                                    driChart.EcartNegatif.Add(Math.Max(0, cibleVal - tauxVal));
                                    driChart.DepassementCible.Add(Math.Max(0, tauxVal - cibleVal));

                                    // Data for tooltips
                                    driChart.TauxAtteintData.Add(tauxVal);
                                    driChart.CibleData.Add(cibleVal);
                                }
                        yearSummary.Charts.Insert(0, driChart); // Add to top of the list
                    }
                }

                if(yearSummary.Charts.Any())
                {
                    viewModel.YearlySummaries.Add(yearSummary);
                }
            }

            // --- 6. DIW Comparison Table Data (Existing) ---
            var lastValidatedSituationIds = allManagedSituations
                .Where(s => s.Statut == 3)
                .GroupBy(s => s.DIW)
                .Select(g => g.OrderByDescending(s => s.DRIValidationDate).First().IDSituation)
                .ToList();

            var declarationsTable = await _context.Declarations
                .Where(d => lastValidatedSituationIds.Contains(d.IDSituation))
                .Include(d => d.Situation)
                .ToListAsync();

            var declarationsByDiw = declarationsTable.ToLookup(d => d.Situation.DIW);

            foreach (var diw in managedDiws)
            {
                int currentYear = DateTime.Now.Year;
                int count = situationsThisYear.Count(s => s.DIW == diw.CodeDIW && s.Year == currentYear.ToString());
                int totalSituationsCount = allManagedSituations.Count(s =>s.DIW == diw.CodeDIW && s.Year == currentYear.ToString());
                var diwData = new DiwComparisonViewModel
                {                                                                   
                    DiwName = diw.LibelleDIW,
                    TotalSituationsCount = totalSituationsCount,
                    PendingSituationsCount = allManagedSituations.Count(s => s.DIW == diw.CodeDIW && s.Statut == 1 && s.Year == currentYear.ToString()),
                    RejectedSituationsCount = allManagedSituations.Count(s => s.DIW == diw.CodeDIW && (s.Statut == 0 ||s.Statut == 2) && s.Year == currentYear.ToString()),
                    ValideSituationsCount = allManagedSituations.Count(s => s.DIW == diw.CodeDIW && s.Statut == 3 && s.Year == currentYear.ToString()),
                    ManqueSituationsCount = Math.Max(0, targetMonth - totalSituationsCount)
                };


                diwData.OverallPerformance = (targetMonth > 0) ? ((double)count / targetMonth) * 100 : 0;

                viewModel.DiwComparisonData.Add(diwData);
            }

            return View(viewModel);
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
        public async Task<IActionResult> Indicateurs()
        {
            var driCode = User.FindFirstValue("CodeDIW");
            var managedDiwCodes = await _context.DIWs
                                        .Where(d => d.CodeDRI == driCode)
                                        .Select(d => d.CodeDIW)
                                        .ToListAsync();

            var allManagedSituations = await _context.Situations
                                     .Where(s => managedDiwCodes.Contains(s.DIW))
                                     .Include(s => s.DIWNavigation)
                                     .OrderByDescending(s => s.CreateDate)
                                     .ToListAsync();

            return View(allManagedSituations);
        }


        public async Task<IActionResult> ReviewSituation(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest();
            }

            var situation = await _context.Situations
                .Include(s => s.DIWNavigation)
                .Include(s => s.RejectionHistories)
                .FirstOrDefaultAsync(s => s.IDSituation == id);

            if (situation == null)
            {
                return NotFound();
            }

            if (!await IsUserAuthorizedForSituation(situation))
            {
                return Forbid();
            }

            var declarations = await _context.Declarations
                .Where(d => d.IDSituation == id)
                .Include(d => d.Indicateur)
                .ThenInclude(i => i.CategorieIndicateur)
                .ToListAsync();

            var viewModel = new ReviewSituationViewModel
            {
                Situation = situation,
                IndicatorGroups = declarations
                    .GroupBy(d => d.Indicateur.CategorieIndicateur)
                    .OrderBy(g => g.Key.IdCategIn)
                    .Select(g => new CategoryIndicatorGroup
                    {
                        CategoryName = g.Key.IntituleCategIn,
                        Declarations = g.OrderBy(d => d.Indicateur.IntituleIn).ToList()
                    }).ToList()
            };

            return View(viewModel);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmSituation(string id)
        {
            var situation = await _context.Situations.FindAsync(id);
            if (situation == null) return NotFound();

            if (!await IsUserAuthorizedForSituation(situation))
            {
                return Forbid();
            }

            if (situation.Statut != 1)
            {
                TempData["ErrorMessage"] = "Cette situation ne peut pas être validée.";
                return RedirectToAction("Indicateurs");
            }

            situation.Statut = 3;
            situation.DRIValidationDate = DateTime.Now;
            _context.Update(situation);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"La situation pour {situation.Month} {situation.Year} a été validée avec succès.";
            return RedirectToAction("Indicateurs");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectSituation(string id, string comment)
        {
            var situation = await _context.Situations.FindAsync(id);
            if (situation == null) return NotFound();

            if (!await IsUserAuthorizedForSituation(situation)) return Forbid();

            if (string.IsNullOrWhiteSpace(comment))
            {
                TempData["ErrorMessage"] = "Le motif du rejet est obligatoire.";
                return RedirectToAction("ReviewSituation", new { id = id });
            }

            if (situation.Statut != 1)
            {
                TempData["ErrorMessage"] = "Cette situation ne peut pas être rejetée.";
                return RedirectToAction("Indicateurs");
            }

            var declarationsToReject = await _context.Declarations
                .Where(d => d.IDSituation == id)
                .ToListAsync();

            foreach (var decl in declarationsToReject)
            {
                var draft = new DeclarationDraft
                {
                    IDSituation = decl.IDSituation,
                    IdIn = decl.IdIn,
                    Numerateur = (float?)decl.Numerateur,
                    Denominateur = (float?)decl.Denominateur,
                    Cible = (float)(decl.Cible ?? 0),
                    Taux = (float?)(decl.taux ?? 0),
                    Ecart = (float?)(decl.ecart ?? 0)
                };
                _context.DeclarationDrafts.Add(draft);
            }

            _context.Declarations.RemoveRange(declarationsToReject);

            var historyEntry = new RejectionHistory
            {
                IDSituation = id,
                Comment = comment,
                RejectionDate = DateTime.Now,
                RejectedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            };
            _context.RejectionHistories.Add(historyEntry);

            situation.Statut = 2;
            situation.EditDate = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"La situation pour {situation.Month} {situation.Year} a été rejetée et renvoyée au DIW.";
            return RedirectToAction("Indicateurs");
        }

        private async Task<bool> IsUserAuthorizedForSituation(Situation situation)
        {
            var driCode = User.FindFirstValue("CodeDIW");
            var managedDiwCodes = await _context.DIWs
                                        .Where(d => d.CodeDRI == driCode)
                                        .Select(d => d.CodeDIW)
                                        .ToListAsync();
            return managedDiwCodes.Contains(situation.DIW);
        }

        public async Task<IActionResult> ExportToExcel(string id)
        {
            var (fileContents, fileName) = await _reportService.GenerateOperationalExcelAsync(id);
            return File(fileContents, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        public async Task<IActionResult> ExportToPdf(string id)
        {
            var (fileContents, fileName) = await _reportService.GenerateOperationalPdfAsync(id);
            return File(fileContents, "application/pdf", fileName);
        }

        public async Task<IActionResult> ExportToExceldri(string id)
        {
            var (fileContents, fileName) = await _reportService.GenerateDriExcelAsync(id);
            return File(fileContents, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        public async Task<IActionResult> ExportToPdfdri(string id)
        {
            var (fileContents, fileName) = await _reportService.GenerateDriPdfAsync(id);
            return File(fileContents, "application/pdf", fileName);
        }

        // In Controllers/DRIController.cs

        public async Task<IActionResult> DIWs()
        {
            var driCode = User.FindFirstValue("CodeDIW");

            var managedDiws = await _context.DIWs
                .Where(d => d.CodeDRI == driCode)
                .OrderBy(d => d.LibelleDIW)
                .ToListAsync();

            var managedDiwCodes = managedDiws.Select(d => d.CodeDIW).ToList();

            // ✨ THIS QUERY IS NOW CORRECT ✨
            // It finds users who are in the managed DIWs AND have the specific DIW User role.
            var usersInDiws = await _context.Users
                .Where(u => u.CodeDIW != null &&
                            managedDiwCodes.Contains(u.CodeDIW) &&
                            u.Statut == (int)UserRole.DIW)
                .ToListAsync();

            var usersByDiw = usersInDiws.ToLookup(u => u.CodeDIW);

            var viewModel = managedDiws.Select(diw => new DIWUsersViewModel
            {
                CodeDIW = diw.CodeDIW,
                LibelleDIW = diw.LibelleDIW,
                Users = usersByDiw[diw.CodeDIW].Select(u => new UserViewModel
                {
                    FullName = $"{u.FirstNmUser} {u.LastNmUser}",
                    Email = u.MailUser,
                    Phone = u.TelUser,
                    LastConnection = u.LastCnx
                }).ToList()
            }).ToList();

            return View(viewModel);
        }


        [HttpGet]
        public async Task<IActionResult> AnalyseOperationnelle(NiveauOpDRIViewModel filters)
        {
            var driCode = User.FindFirstValue("CodeDIW");
            var managedDiwCodes = await _context.DIWs
                                                .Where(d => d.CodeDRI == driCode)
                                                .Select(d => d.CodeDIW)
                                                .ToListAsync();

            var viewModel = new NiveauOpDRIViewModel
            {
                // 1. Populate filter dropdowns
                YearOptions = await _context.Situations.Select(s => s.Year).Distinct().OrderByDescending(y => y).Select(y => new SelectListItem { Value = y, Text = y }).ToListAsync(),
                AxeOptions = await _context.CategorieIndicateurs.OrderBy(c => c.IntituleCategIn).Select(c => new SelectListItem { Value = c.IdCategIn, Text = c.IntituleCategIn }).ToListAsync(),
                IndicateurOptions = await _context.Indicateurs
                    .Where(i => string.IsNullOrEmpty(filters.SelectedAxe) || i.IdCategIn == filters.SelectedAxe)
                    .OrderBy(i => i.IntituleIn)
                    .Select(i => new SelectListItem { Value = i.IdIn, Text = i.IntituleIn })
                    .ToListAsync(),

                // Restore selected filter values
                SelectedYear = filters.SelectedYear,
                SelectedSemester = filters.SelectedSemester,
                SelectedTrimester = filters.SelectedTrimester,
                SelectedMonth = filters.SelectedMonth,
                SelectedAxe = filters.SelectedAxe,
                SelectedIndicateur = filters.SelectedIndicateur,
                IsSearchPerformed = true // For DRI, we always search
            };

            // 2. Build the query
            IQueryable<Declaration> query = _context.Declarations
                .Include(d => d.Situation)
                .Include(d => d.Indicateur)
                .ThenInclude(i => i.CategorieIndicateur)
                // Core logic: only include declarations from DIWs managed by this DRI
                .Where(d => managedDiwCodes.Contains(d.Situation.DIW));

            // Apply filters
            if (!string.IsNullOrEmpty(filters.SelectedYear)) query = query.Where(d => d.Situation.Year == filters.SelectedYear);
            if (!string.IsNullOrEmpty(filters.SelectedAxe)) query = query.Where(d => d.Indicateur.IdCategIn == filters.SelectedAxe);
            if (!string.IsNullOrEmpty(filters.SelectedIndicateur)) query = query.Where(d => d.IdIn == filters.SelectedIndicateur);

            // Date filters
            if (filters.SelectedMonth.HasValue)
            {
                string monthName = new DateTime(2000, filters.SelectedMonth.Value, 1).ToString("MMMM", new CultureInfo("fr-FR"));
                query = query.Where(d => d.Situation.Month == monthName);
            }
            if (filters.SelectedTrimester.HasValue)
            {
                int startMonth = (filters.SelectedTrimester.Value - 1) * 3 + 1;
                var months = Enumerable.Range(startMonth, 3).Select(m => new DateTime(2000, m, 1).ToString("MMMM", new CultureInfo("fr-FR"))).ToList();
                query = query.Where(d => months.Contains(d.Situation.Month));
            }
            if (filters.SelectedSemester.HasValue)
            {
                int startMonth = (filters.SelectedSemester.Value - 1) * 6 + 1;
                var months = Enumerable.Range(startMonth, 6).Select(m => new DateTime(2000, m, 1).ToString("MMMM", new CultureInfo("fr-FR"))).ToList();
                query = query.Where(d => months.Contains(d.Situation.Month));
            }

            // 3. Fetch, process, and calculate results
            var filteredDeclarations = await query.ToListAsync();
            viewModel.PerformanceResults = filteredDeclarations
                .GroupBy(d => d.Indicateur)
                .Select(g =>
                {
                    double totalNumerateur = g.Sum(d => d.Numerateur ?? 0);
                    double totalDenominateur = g.Sum(d => d.Denominateur ?? 0);
                    double taux = (totalDenominateur > 0) ? (totalNumerateur / totalDenominateur) * 100 : 0;
                    
                    return new IndicatorPerformanceDRIViewModel
                    {
                        AxeName = g.Key.CategorieIndicateur.IntituleCategIn,
                        IndicatorName = g.Key.IntituleIn,
                        SumNumerateur = totalNumerateur,
                        SumDenominateur = totalDenominateur,
                        Taux = taux,
                    };
                })
                .OrderBy(r => r.AxeName).ThenBy(r => r.IndicatorName)
                .ToList();

            return View(viewModel);
        }

        // Helper API to get indicators for the dynamic dropdown
        [HttpGet]
        public async Task<JsonResult> GetIndicatorsByAxeJson(string axeId)
        {
            var query = _context.Indicateurs.AsQueryable();

            if (!string.IsNullOrEmpty(axeId))
            {
                query = query.Where(i => i.IdCategIn == axeId);
            }

            var indicators = await query
                .OrderBy(i => i.IntituleIn)
                .Select(i => new { value = i.IdIn, text = i.IntituleIn })
                .ToListAsync();

            return Json(indicators);
        }


    

        // ACTION 2: Handles the form submission to create a new situation.
        // Inside DRIController.cs

        // In Controllers/DRIController.cs

        
        #region DRI Self-Reporting Actions

        [HttpGet]
        public async Task<IActionResult> SaisieIndicateurs()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) { return Challenge(); }
            int currentYear = DateTime.Now.Year;
            var yearList = new List<int> { currentYear - 1, currentYear, currentYear + 1 };
            ViewBag.YearOptions = yearList.Select(y => new SelectListItem
            {
                Text = y.ToString(),
                Value = y.ToString(),
                Selected = (y == currentYear) // Pre-select the current year
            }).ToList();
            var driSituations = await _context.Situations
                .Where(s => s.User_id == currentUserId)
                .OrderByDescending(s => s.Year)
                .ThenByDescending(s => s.Month)
                .ToListAsync();
            return View("_EditerDRI", driSituations);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreerSituationDRI(string month, string year)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) { return Challenge(); }

            var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUser == null || string.IsNullOrEmpty(currentUser.CodeDIW))
            {
                TempData["ErrorMessage"] = "Erreur: Utilisateur ou CodeDIW non trouvé.";
                return RedirectToAction("SaisieIndicateurs");
            }

            if (await _context.Situations.AnyAsync(s => s.Month == month && s.Year == year && s.User_id == currentUserId))
            {
                TempData["ErrorMessage"] = "Une situation pour cette période existe déjà.";
                return RedirectToAction("SaisieIndicateurs");
            }

            var situation = new Situation
            {
                IDSituation = Guid.NewGuid().ToString(),
                Month = month,
                Year = year,
                Statut = 0,
                CreateDate = DateTime.Now,
                User_id = currentUserId,
                DIW = currentUser.CodeDIW
            };
            _context.Situations.Add(situation);
            await _context.SaveChangesAsync();
            return RedirectToAction("SaisirSituation", new { id = situation.IDSituation });
        }

        [HttpGet]
        public async Task<IActionResult> SaisirSituation(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            // ==================== MODIFICATIONS START HERE ====================

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // The query is updated to include rejection history for display.
            var situation = await _context.Situations
                .Include(s => s.RejectionHistories)
                .FirstOrDefaultAsync(s => s.IDSituation == id);

            if (situation == null) return NotFound();

            // 1. Add Ownership Check: Deny access if the user is not the owner.
            if (situation.User_id != currentUserId)
            {
                return Forbid();
            }

            // 2. Add Status Check: Allow editing only for status 0 (new) or 2 (rejected).
            if (situation.Statut != 0 && situation.Statut != 2)
            {
                // For any other status, redirect to the read-only "Consulter" page.
                TempData["InfoMessage"] = "Cette situation est déjà soumise ou validée et ne peut plus être modifiée.";
                return RedirectToAction("ConsulterSituation", new { id = id });
            }

            // ===================== MODIFICATIONS END HERE =====================


            // Your original logic for fetching data remains unchanged.
            var userCodeDri = User.FindFirstValue("CodeDIW");
            if (string.IsNullOrEmpty(userCodeDri)) return Forbid();

            var driIndicators = await _context.Indicateurs_DE_PERFORMANCE_OPERATIONNELS
                .Where(i => i.IdIndicacteur == 5 || i.IdIndicacteur == 6 || i.IdIndicacteur == 7)
                .ToListAsync();

            var targets = await _context.cibles_de_performance_dri
                .Where(c => c.year.Trim() == situation.Year.Trim() && c.CodeDRI.Trim() == userCodeDri.Trim())
                .ToDictionaryAsync(
                    c => c.IdIndicacteur,
                    c => (float)c.cible
                );

            var drafts = await _context.DeclarationDRIDrafts
                .Where(d => d.IDSituation == id)
                .ToDictionaryAsync(d => d.IdIndicacteur);

            ViewBag.Situation = situation;
            ViewBag.Indicators = driIndicators;
            ViewBag.Targets = targets;
            ViewBag.Drafts = drafts;

            return View("_ValiditeDRI");
        }

        [HttpGet]
        public async Task<IActionResult> ConsulterSituation(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var situation = await _context.Situations.FindAsync(id);
            if (situation == null) return NotFound();

            var driIndicators = await _context.Indicateurs_DE_PERFORMANCE_OPERATIONNELS
                .Where(i => i.IdIndicacteur == 5 || i.IdIndicacteur == 6 || i.IdIndicacteur == 7)
                .ToListAsync();

            var declarations = await _context.DeclarationDRIs
                .Where(d => d.IDSituation == id)
                .ToDictionaryAsync(d => d.IdIndicacteur);

            ViewBag.Situation = situation;
            ViewBag.Indicators = driIndicators;
            ViewBag.Declarations = declarations;
            return View("_ConsulterDRI");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnregistrerBrouillonDRI(string IDSituation, Dictionary<int, IndicatorInputModelDRI> Indicators)
        {
            var situation = await _context.Situations.FindAsync(IDSituation);
            if (situation == null) return NotFound();
            var userCodeDri = User.FindFirstValue("CodeDIW");
            if (string.IsNullOrEmpty(userCodeDri)) return Forbid();

            var existingDrafts = await _context.DeclarationDRIDrafts
                .Where(d => d.IDSituation == IDSituation)
                .ToDictionaryAsync(d => d.IdIndicacteur);

            // Correctly filters by both year AND the user's DRI code, ignoring whitespace.
            var targets = await _context.cibles_de_performance_dri
    .Where(c => c.year.Trim() == situation.Year.Trim() && c.CodeDRI.Trim() == userCodeDri.Trim())
    .ToDictionaryAsync(
        c => c.IdIndicacteur,
        c => (float)c.cible  // <-- Explicit cast added here
    );

            foreach (var indicatorEntry in Indicators)
            {
                var idIndicacteur = indicatorEntry.Key;
                var input = indicatorEntry.Value;
                if (input.Numerateur == null && input.Denominateur == null) continue;

                double? taux = null, ecart = null;
                double cible = targets.GetValueOrDefault(idIndicacteur);

                if (input.Numerateur.HasValue && input.Denominateur.HasValue && input.Denominateur != 0)
                {
                    taux = (input.Numerateur / input.Denominateur) * 100;
                    ecart = taux - cible;
                }

                if (existingDrafts.TryGetValue(idIndicacteur, out var draft))
                {
                    draft.Numerateur = (float?)input.Numerateur;
                    draft.Denominateur = (float?)input.Denominateur;
                    draft.taux = (float)taux;
                    draft.ecart = (float)ecart;
                    draft.Cible = (float)cible;
                }
                else
                {
                    _context.DeclarationDRIDrafts.Add(new DeclarationDRIDraft
                    {
                        IDSituation = IDSituation,
                        IdIndicacteur = idIndicacteur,
                        Numerateur = (float?)input.Numerateur,
                        Denominateur = (float?)input.Denominateur,
                        taux = (float)taux,
                        ecart = (float)ecart,
                        Cible = (float)cible
                    });
                }
            }
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Brouillon enregistré avec succès !";
            return RedirectToAction("SaisieIndicateurs");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmerSituationDRI(string IDSituation, Dictionary<int, IndicatorInputModelDRI> Indicators)
        {
            var situation = await _context.Situations.FindAsync(IDSituation);
            if (situation == null) return NotFound();
            var userCodeDri = User.FindFirstValue("CodeDIW");
            if (string.IsNullOrEmpty(userCodeDri)) return Forbid();

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        var draftsToDelete = await _context.DeclarationDRIDrafts.Where(d => d.IDSituation == IDSituation).ToListAsync();
                        if (draftsToDelete.Any()) _context.DeclarationDRIDrafts.RemoveRange(draftsToDelete);

                        var declarationsToDelete = await _context.DeclarationDRIs.Where(d => d.IDSituation == IDSituation).ToListAsync();
                        if (declarationsToDelete.Any()) _context.DeclarationDRIs.RemoveRange(declarationsToDelete);

                        // Correctly filters by both year AND the user's DRI code, ignoring whitespace.
                        var targets = await _context.cibles_de_performance_dri
                            .Where(c => c.year.Trim() == situation.Year.Trim() && c.CodeDRI.Trim() == userCodeDri.Trim())
                            .ToDictionaryAsync(c => c.IdIndicacteur, c => c.cible);

                        foreach (var indicatorEntry in Indicators)
                        {
                            var idIndicacteur = indicatorEntry.Key;
                            var input = indicatorEntry.Value;
                            if (input.Numerateur == null && input.Denominateur == null) continue;

                            double? taux = null, ecart = null;
                            double cible = targets.GetValueOrDefault(idIndicacteur);
                            if (input.Numerateur.HasValue && input.Denominateur.HasValue && input.Denominateur != 0)
                            {
                                taux = (input.Numerateur / input.Denominateur) * 100;
                                ecart = taux - cible;
                            }
                            _context.DeclarationDRIs.Add(new DeclarationDRI
                            {
                                IDSituation = IDSituation,
                                IdIndicacteur = idIndicacteur,
                                Numerateur = (float)input.Numerateur,
                                Denominateur = (float)input.Denominateur,
                                taux = (float)taux,
                                ecart = (float)ecart,
                                Cible = (float)cible
                            });
                        }
                        situation.Statut = 1;
                        situation.ConfirmDate = DateTime.Now;
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        TempData["SuccessMessage"] = "Situation confirmée et envoyée avec succès !";
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        TempData["ErrorMessage"] = "Erreur de base de données : " + ex.Message;
                    }
                }
            });

            if (TempData.ContainsKey("SuccessMessage")) return RedirectToAction("SaisieIndicateurs");
            else return RedirectToAction("SaisirSituation", new { id = IDSituation });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSituationDRI(string id)
        {
            if (string.IsNullOrEmpty(id)) return BadRequest();
            var situation = await _context.Situations.FindAsync(id);
            if (situation == null) return NotFound();

            if (situation.Statut != 0)
            {
                TempData["ErrorMessage"] = "Impossible de supprimer une situation qui a déjà été valide.";
                return RedirectToAction("SaisieIndicateurs");
            }

            var relatedDrafts = await _context.DeclarationDRIDrafts.Where(d => d.IDSituation == id).ToListAsync();
            if (relatedDrafts.Any())
            {
                _context.DeclarationDRIDrafts.RemoveRange(relatedDrafts);
            }

            _context.Situations.Remove(situation);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"La situation pour {situation.Month} {situation.Year} a été supprimée avec succès.";
            return RedirectToAction("SaisieIndicateurs");
        }

        // --- ADD TO DRIController.cs ---

        [HttpGet] // Changed from HttpPost to HttpGet
                  // [ValidateAntiForgeryToken] REMOVED
        public async Task<IActionResult> ExportAnalyseOperationnelle(NiveauOpDRIViewModel filters)
        {
            var driCode = User.FindFirstValue("CodeDIW");
            var managedDiwCodes = await _context.DIWs.Where(d => d.CodeDRI == driCode).Select(d => d.CodeDIW).ToListAsync();

            IQueryable<Declaration> query = _context.Declarations
                .Include(d => d.Situation).Include(d => d.Indicateur).ThenInclude(i => i.CategorieIndicateur)
                .Where(d => managedDiwCodes.Contains(d.Situation.DIW));

            if (!string.IsNullOrEmpty(filters.SelectedYear)) query = query.Where(d => d.Situation.Year == filters.SelectedYear);
            if (!string.IsNullOrEmpty(filters.SelectedAxe)) query = query.Where(d => d.Indicateur.IdCategIn == filters.SelectedAxe);
            if (!string.IsNullOrEmpty(filters.SelectedIndicateur)) query = query.Where(d => d.IdIn == filters.SelectedIndicateur);

            if (filters.SelectedMonth.HasValue)
            {
                string monthName = new DateTime(2000, filters.SelectedMonth.Value, 1).ToString("MMMM", new CultureInfo("fr-FR"));
                query = query.Where(d => d.Situation.Month == monthName);
            }
            else if (filters.SelectedTrimester.HasValue)
            {
                int startMonth = (filters.SelectedTrimester.Value - 1) * 3 + 1;
                var months = Enumerable.Range(startMonth, 3).Select(m => new DateTime(2000, m, 1).ToString("MMMM", new CultureInfo("fr-FR"))).ToList();
                query = query.Where(d => months.Contains(d.Situation.Month));
            }
            else if (filters.SelectedSemester.HasValue)
            {
                int startMonth = (filters.SelectedSemester.Value - 1) * 6 + 1;
                var months = Enumerable.Range(startMonth, 6).Select(m => new DateTime(2000, m, 1).ToString("MMMM", new CultureInfo("fr-FR"))).ToList();
                query = query.Where(d => months.Contains(d.Situation.Month));
            }

            var filteredDeclarations = await query.ToListAsync();
            var results = filteredDeclarations
                .GroupBy(d => d.Indicateur)
                .Select(g =>
                {
                    double totalNumerateur = g.Sum(d => d.Numerateur ?? 0);
                    double totalDenominateur = g.Sum(d => d.Denominateur ?? 0);
                    return new IndicatorPerformanceDRIViewModel
                    {
                        AxeName = g.Key.CategorieIndicateur.IntituleCategIn,
                        IndicatorName = g.Key.IntituleIn,
                        SumNumerateur = totalNumerateur,
                        SumDenominateur = totalDenominateur,
                        Taux = (totalDenominateur > 0) ? (totalNumerateur / totalDenominateur) * 100 : 0
                    };
                })
                .OrderBy(r => r.AxeName).ThenBy(r => r.IndicatorName)
                .ToList();

            var (fileContents, fileName) = await _reportService.GenerateAnalysisDriExcelAsync(results, filters);
            return File(fileContents, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        #endregion
    }

    public class IndicatorInputModelDRI
    {
        public double? Numerateur { get; set; }
        public double? Denominateur { get; set; }
    }

}