using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stat.Models;
using System.Diagnostics;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Stat.Services;
using Stat.Models.ViewModels;

namespace Stat.Controllers
{
    [Authorize(Policy = "DIWAccess")]
    public class DIWController : BaseController
    {
        private readonly DatabaseContext _context;
        private readonly IReportService _reportService;
        private readonly IWebHostEnvironment _hostEnvironment;

        public DIWController(DatabaseContext context, IWebHostEnvironment hostEnvironment, IReportService reportService)
            : base(context, hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
            _reportService = reportService;
        }

        // YEAR_MOD: Updated helper to fetch targets for a specific year.
        private async Task<IndexViewModel> CreateViewModelForDataEntry(string situationYear)
        {
            var userDiw = User.FindFirstValue("CodeDIW");
            if (string.IsNullOrEmpty(userDiw))
            {
                return new IndexViewModel { IndicatorsWithCibles = new List<IndicatorWithCibleViewModel>() };
            }

            // This query now joins indicators with targets for the specific DIW and YEAR.
            var rawData = await (from ind in _context.Indicateurs
                                 join cib in _context.cibles on ind.IdIn equals cib.IdIn
                                 where cib.CodeDIW == userDiw && cib.year == situationYear
                                 select new
                                 {
                                     ind.IdIn,
                                     ind.IntituleIn,
                                     CibleValue = cib.cible,
                                     ind.IdCategIn
                                 }).ToListAsync();

            var indicatorsData = rawData.Select(item => new IndicatorWithCibleViewModel
            {
                IdIn = item.IdIn,
                IntituleIn = item.IntituleIn,
                CibleValue = item.CibleValue,
                IdCategIn = item.IdCategIn
            }).ToList();

            var categories = await _context.CategorieIndicateurs.ToListAsync();

            return new IndexViewModel
            {
                IndicatorsWithCibles = indicatorsData,
                CategorieIndicateurs = categories
            };
        }

        public async Task<IActionResult> Index()
        {
            var userDiw = User.FindFirstValue("CodeDIW");
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var viewModel = new DashboardViewModel();
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
                    .Where(r => r.CodeStructure == userDiw && r.Type == rapportType && r.Year == rapportYear)
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
            var currentUser = await _context.Users.FindAsync(userId);

            // Fetch all situations for the entire DIW to calculate team-based KPI cards
            var allDiwSituations = await _context.Situations.Where(s => s.DIW == userDiw).ToListAsync();

            // Fetch situations for ONLY the current user to generate their personal alerts
            var mySituations = allDiwSituations.Where(s => s.User_id == userId).ToList();

            // --- 1. KPI Card Calculations (Team-Wide) ---
            viewModel.SituationsInProgress = allDiwSituations.Count(s => s.Statut == 0 || s.Statut == 2);
            viewModel.SituationsPendingDRI = allDiwSituations.Count(s => s.Statut == 1);
            viewModel.SituationsValidated = allDiwSituations.Count(s => s.Statut == 3);
            viewModel.GrandTotalSituations = allDiwSituations.Count();

            var situationsByYear = allDiwSituations.GroupBy(s => s.Year).OrderByDescending(g => g.Key);
            foreach (var yearGroup in situationsByYear)
            {
                if (!int.TryParse(yearGroup.Key, out int currentYear)) continue;

                var yearSummary = new YearlySummaryViewModel
                {
                    Year = currentYear,
                    TotalSituations = yearGroup.Count(),
                    ConfirmedSituations = yearGroup.Count(s => s.Statut == 1 || s.Statut == 3),
                    PendingSituations = yearGroup.Count(s => s.Statut == 0 || s.Statut == 2)
                };

                // Find the latest confirmed situation FOR THIS SPECIFIC YEAR to generate charts
                var latestConfirmedSituationInYear = yearGroup
                    .Where(s => s.Statut == 3)
                    .OrderByDescending(s => GetMonthNumber(s.Month))
                    .FirstOrDefault();

                if (latestConfirmedSituationInYear != null)
                {
                    var declarationsForLatestSituation = await _context.Declarations
                        .Include(d => d.Indicateur).ThenInclude(i => i.CategorieIndicateur)
                        .Where(d => d.IDSituation == latestConfirmedSituationInYear.IDSituation)
                        .ToListAsync();

                    var declarationsByCategory = declarationsForLatestSituation.GroupBy(d => d.Indicateur.CategorieIndicateur);

                    foreach (var categoryGroup in declarationsByCategory)
                    {
                        var chart = new DashboardChartViewModel
                        {
                            CategoryName = $"{categoryGroup.Key.IntituleCategIn} (Situation de {latestConfirmedSituationInYear.Month})",
                            ChartId = $"chart_{yearSummary.Year}_{categoryGroup.Key.IdCategIn}"
                        };

                        foreach (var declaration in categoryGroup.OrderBy(d => d.Indicateur.IntituleIn))
                        {
                            chart.Labels.Add(declaration.Indicateur.IntituleIn);
                            double taux = declaration.taux ?? 0;
                            double cible = declaration.Cible ?? 0;

                            double performance = Math.Min(taux, cible);
                            double ecartNegatif = Math.Max(0, cible - taux);
                            double depassement = Math.Max(0, taux - cible);

                            chart.PerformanceJusquaCible.Add(performance);
                            chart.EcartNegatif.Add(ecartNegatif);
                            chart.DepassementCible.Add(depassement);
                            chart.TauxAtteintData.Add(taux);
                            chart.CibleData.Add(cible);
                        }
                        yearSummary.Charts.Add(chart);
                    }
                }
                viewModel.YearlySummaries.Add(yearSummary);
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

        public IActionResult Privacy() => View();

        public async Task<IActionResult> LogOut()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Access");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // ✨ UPDATE `_Editer` (GET) ACTION
        public async Task<IActionResult> _Editer()
        {
            var userDiw = User.FindFirstValue("CodeDIW");
            var situations = await _context.Situations
                                         .Include(s => s.RejectionHistories)
                                         .Where(s => s.DIW == userDiw)
                                         .ToListAsync();

            var sortedSituations = situations
                    .OrderByDescending(s => s.EditDate ?? s.CreateDate)
                    .ToList();

            // ✨ ADD THIS: Get the current user's ID to pass to the view for ownership checks
            ViewBag.CurrentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // ✨ ADD THIS: Get all users in the same DIW to display their names in the list
            var usersInDiw = await _context.Users
                .Where(u => u.CodeDIW == userDiw)
                .ToDictionaryAsync(u => u.ID_User, u => $"{u.FirstNmUser} {u.LastNmUser}");
            ViewBag.DiwUsers = usersInDiw;

            var viewModel = new IndexViewModel { Situations = sortedSituations };
            return View("_Editer", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> _Editer(string Month, string Year)
        {
            if (string.IsNullOrEmpty(Month) || string.IsNullOrEmpty(Year))
            {
                TempData["Message"] = "Veuillez sélectionner un mois et une année.";
                return RedirectToAction(nameof(_Editer));
            }

            var userDiw = User.FindFirstValue("CodeDIW");
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            bool exists = await _context.Situations
                .AnyAsync(s => s.DIW == userDiw && s.Month == Month && s.Year == Year);

            if (exists)
            {
                TempData["Message"] = "Une situation pour ce mois et cette année existe déjà.";
                return RedirectToAction(nameof(_Editer));
            }

            var newSituation = new Situation
            {
                IDSituation = Guid.NewGuid().ToString(),
                Month = Month,
                Year = Year,
                DIW = userDiw,
                User_id = userId,
                CreateDate = DateTime.Now,
                Statut = 0
            };

            _context.Situations.Add(newSituation);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(_Editer));
        }

        // YEAR_MOD: This action now passes the situation's year to the helper method.
        public async Task<IActionResult> _Validite(string id)
        {
            if (id == null) return NotFound();

            var situation = await _context.Situations
                .Include(s => s.RejectionHistories)
                .FirstOrDefaultAsync(s => s.IDSituation == id);

            if (situation == null) return NotFound();

            // 🛑 SECURITY CHECK 🛑
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (situation.User_id != currentUserId)
            {
                return Forbid(); // Deny access if not the owner
            }
            // End of security check

            var drafts = await _context.DeclarationDrafts
                .Where(d => d.IDSituation == id)
                .ToDictionaryAsync(d => d.IdIn, d => d);

            ViewBag.Drafts = drafts;
            ViewBag.CurrentSituationId = id;
            ViewBag.Situation = situation;

            // Pass the situation's year to fetch the correct targets.
            var viewModel = await CreateViewModelForDataEntry(situation.Year);

            return View("_Validite", viewModel);
        }

        public async Task<IActionResult> Delete(string? id)
        {
            if (id == null) return NotFound();
            var situation = await _context.Situations.FindAsync(id);
            if (situation == null) return NotFound();
            return View(situation);
        }

        // ✨ SECURE `DeleteConfirmed` ACTION
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(String id)
        {
            var situation = await _context.Situations.FindAsync(id);
            if (situation == null) return NotFound();

            // 🛑 SECURITY CHECK 🛑
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (situation.User_id != currentUserId)
            {
                return Forbid(); // Deny access if not the owner
            }
            // End of security check

            _context.Situations.Remove(situation);
            situation.DeleteDate = DateTime.Now;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(_Editer));
        }

        public async Task<IActionResult> _Saisir()
        {
            // YEAR_MOD: This view isn't tied to a specific situation, so we load the current year's targets by default.
            var viewModel = await CreateViewModelForDataEntry(DateTime.Now.Year.ToString());
            return PartialView("_Saisir", viewModel);
        }

        public ActionResult Valid(Guid id)
        {
            ViewBag.Id = id;
            return View();
        }

        [HttpPost]
        public ActionResult _Saisir(Situation std) => RedirectToAction("_Editer");

        [HttpPost]
        public IActionResult CalculerCumulTaux([FromBody] IndexViewModel model)
        {
            int l = model.ligne;
            try
            {
                if (model == null) return Json(new { error = true, message = "Le modèle est null." });
                if (model.ValeurMois <= 0 || model.ValeurCible <= 0) return Json(new { error = true, message = "Valeurs invalides" });
                double cumule = model.ValeurMois + model.cumule;
                double taux = (cumule / model.ValeurCible) * 100;
                return Json(new { error = false, cumule = cumule, taux = taux });
            }
            catch (Exception ex)
            {
                return Json(new { error = true, message = "Exception: " + ex.Message });
            }
        }

        // ✨ SECURE `EnregistrerBrouillon` (Save Draft) ACTION
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnregistrerBrouillon(string id, List<IndicatorInputModel> indicators)
        {
            var situation = await _context.Situations.FindAsync(id);
            if (situation == null) return NotFound();

            // 🛑 SECURITY CHECK 🛑
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (situation.User_id != currentUserId)
            {
                return Forbid(); // Deny access if not the owner
            }
            // End of security check

            var oldDrafts = _context.DeclarationDrafts.Where(d => d.IDSituation == id);
            if (oldDrafts.Any())
            {
                _context.DeclarationDrafts.RemoveRange(oldDrafts);
            }

            foreach (var indicator in indicators)
            {
                float numerateur = indicator.Numerateur ?? 0;
                float denominateur = indicator.Denominateur ?? 0;
                float cible = (float)indicator.cible;
                float taux = (denominateur != 0) ? (float)Math.Round((numerateur / denominateur) * 100, 2) : 0;
                float ecart = (denominateur != 0) ? (float)Math.Round(taux - cible, 2) : 0 - cible;

                var draft = new DeclarationDraft
                {
                    IDSituation = id,
                    IdIn = indicator.IndicatorId,
                    Numerateur = numerateur,
                    Denominateur = denominateur,
                    Cible = cible,
                    Taux = taux,
                    Ecart = ecart
                };
                _context.DeclarationDrafts.Add(draft);
            }
            situation.EditDate = DateTime.Now;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(_Editer));
        }

        // ✨ SECURE `Confirmer` ACTION
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirmer(string id, List<IndicatorInputModel> indicators)
        {
            var situation = await _context.Situations.FindAsync(id);
            if (situation == null) return NotFound();

            // 🛑 SECURITY CHECK 🛑
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (situation.User_id != currentUserId)
            {
                return Forbid(); // Deny access if not the owner
            }
            // End of security check

            if (indicators == null || !indicators.Any())
            {
                TempData["Message"] = "Erreur: Aucune donnée d'indicateur n'a été soumise.";
                return RedirectToAction("_Validite", new { id = id });
            }

            _context.Declarations.RemoveRange(_context.Declarations.Where(d => d.IDSituation == id));
            _context.DeclarationDrafts.RemoveRange(_context.DeclarationDrafts.Where(d => d.IDSituation == id));

            foreach (var indicator in indicators)
            {
                float numerateur = indicator.Numerateur ?? 0;
                float denominateur = indicator.Denominateur ?? 0;
                double cible = (double)indicator.cible;
                double taux = (denominateur != 0) ? (numerateur / denominateur) * 100 : 0;
                double ecart = (denominateur != 0) ? taux - cible : 0 - cible;

                var declaration = new Declaration
                {
                    IDSituation = id,
                    IdIn = indicator.IndicatorId,
                    Numerateur = numerateur,
                    Denominateur = denominateur,
                    Cible = (float?)cible,
                    taux = (float)Math.Round(taux, 2),
                    ecart = (float)Math.Round(ecart, 2)
                };
                _context.Declarations.Add(declaration);
            }

            situation.Statut = 1;
            situation.ConfirmDate = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "La situation a été corrigée et renvoyée pour validation avec succès.";
            return RedirectToAction(nameof(_Editer));
        }

        public async Task<IActionResult> Consulte(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var situation = await _context.Situations.FindAsync(id);
            if (situation == null) return NotFound();

            var userDiw = User.FindFirstValue("CodeDIW");
            if (situation.DIW != userDiw) return Forbid(); // Ensure user is in the same DIW

            var rawData = await (from dec in _context.Declarations
                                 join ind in _context.Indicateurs on dec.IdIn equals ind.IdIn
                                 where dec.IDSituation == id
                                 select new
                                 {
                                     ind.IdIn,
                                     ind.IntituleIn,
                                     dec.Cible,
                                     dec.taux,
                                     dec.ecart,
                                     ind.IdCategIn,
                                     dec.Numerateur,
                                     dec.Denominateur
                                 }).ToListAsync();
            var confirmedData = rawData.Select(item => new IndicatorWithCibleViewModel
            {
                IdIn = item.IdIn,
                IntituleIn = item.IntituleIn,
                CibleValue = (double?)item.Cible ?? 0,
                Taux = (float?)item.taux,
                Ecart = (float?)item.ecart,
                IdCategIn = item.IdCategIn,
                Numerateur = item.Numerateur,
                Denominateur = item.Denominateur
            }).ToList();
            var viewModel = new IndexViewModel
            {
                IndicatorsWithCibles = confirmedData,
                CategorieIndicateurs = await _context.CategorieIndicateurs.ToListAsync()
            };
            return View("Consulte", viewModel);
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
    }
}