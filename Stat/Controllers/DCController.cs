// [File: DCController.cs]
// =============================================================
//      This is the complete and corrected DCController.cs
// =============================================================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stat.Models;
using System.Security.Claims;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Stat.Models.ViewModels;
using DocumentFormat.OpenXml.Spreadsheet;
using Colors = QuestPDF.Helpers.Colors;

namespace Stat.Controllers
{
    [Authorize(Policy = "DCAccess")] // Ensure you have a "DCAccess" policy in Program.cs
    public class DCController : BaseController
    {
        private readonly DatabaseContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;

        public DCController(DatabaseContext context, IWebHostEnvironment hostEnvironment)
            : base(context, hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
        }


        private async Task<DCDataEntryViewModel> BuildHierarchicalViewModel(string situationYear)
        {
            var viewModel = new DCDataEntryViewModel();

            var categories = await _context.CategorieIndicateurs
                .Include(c => c.Objectifs)
                .ThenInclude(o => o.IndicateursStrategiques)
                // ✨ This line is essential to load the yearly targets from the database
                .ThenInclude(i => i.CiblesStrategiques)
                .OrderBy(c => c.IdCategIn)
                .ToListAsync();

            foreach (var category in categories)
            {
                var categoryVm = new CategoryViewModel { CategoryName = category.IntituleCategIn };
                foreach (var objective in category.Objectifs.OrderBy(o => o.idobj))
                {
                    var objectiveVm = new ObjectiveViewModel { ObjectiveName = objective.Intituleobj };
                    foreach (var indicator in objective.IndicateursStrategiques.OrderBy(i => i.IdIndic))
                    {
                        objectiveVm.Indicators.Add(new IndicatorViewModel
                        {
                            IdIndic = indicator.IdIndic,
                            IntituleIn = indicator.IntituleIn,

                            // ✨ THIS IS THE CORRECTED LINE
                            // It queries the CiblesStrategiques collection, not the old Cible property.
                            Cible = indicator.CiblesStrategiques.FirstOrDefault(c => c.year == situationYear)?.cible ?? 0
                        });
                    }
                    categoryVm.Objectives.Add(objectiveVm);
                }
                viewModel.Categories.Add(categoryVm);
            }
            return viewModel;
        }

        // --- PAGE DE SAISIE DES DONNÉES (_Validite) ---
        public async Task<IActionResult> _Validite(string id)
        {
            // SECURITY FIX: Verify ownership
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var situation = await _context.Situations.Include(s => s.RejectionHistories).FirstOrDefaultAsync(s => s.IDSituation == id && s.User_id == userId);
            if (situation == null) return Forbid(); // Use Forbid() for unauthorized access

            ViewBag.Situation = situation;
            ViewBag.CurrentSituationId = id;
            ViewBag.Drafts = await _context.DeclarationsStrategiquesDrafts
                .Where(d => d.IDSituation == id)
                .ToDictionaryAsync(d => d.IdIndic, d => d);

            // YEAR_MOD: Pass the situation's year to the view model builder.
            var viewModel = await BuildHierarchicalViewModel(situation.Year);
            return View(viewModel);
        }

        // --- PAGE DE CONSULTATION (pour DC et Admin) ---
        public async Task<IActionResult> Consulte(string id)
        {
            // SECURITY FIX: Verify ownership
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(id)) return NotFound();
            var situation = await _context.Situations.FindAsync(id);
            if (situation == null) return Forbid();

            var userDiw = User.FindFirstValue("CodeDIW");
            if (situation.DIW != userDiw) return Forbid();
            ViewBag.Situation = situation;

            var declarations = await _context.DeclarationsStrategiques
                 .Where(d => d.IDSituation == id)
                 .ToListAsync();

            ViewBag.Declarations = declarations.ToDictionary(d => d.IdIndic, d => d);

            // YEAR_MOD: Pass the situation's year to the view model builder.
            var viewModel = await BuildHierarchicalViewModel(situation.Year);

            return View(viewModel);
        }

        // [File: DCController.cs]
        // Inside the DCController class

        // 1. UPDATE THE INDEX ACTION (For Dashboard Charts)
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userDiw = User.FindFirstValue("CodeDIW"); // Get the Structure Code

            // Fetch situations for the entire Structure (DC), not just the user
            var allStructureSituations = await _context.Situations
                .Where(s => s.DIW == userDiw)
                .Include(s => s.RejectionHistories)
                .ToListAsync();

            var viewModel = new DashboardViewModel
            {
                // KPI Cards: Based on the whole structure
                SituationsInProgress = allStructureSituations.Count(s => s.Statut == 0 || s.Statut == 2),
                SituationsValidated = allStructureSituations.Count(s => s.Statut == 3),
                GrandTotalSituations = allStructureSituations.Count()
            };

            // Get the IDs of the latest VALIDATED (Statut == 3) situations for the structure
            var latestSituationIds = allStructureSituations
                .Where(s => s.Statut == 3)
                .GroupBy(s => s.Year)
                .Select(g => g.OrderByDescending(s => GetMonthNumber(s.Month)).First().IDSituation)
                .ToList();

            // Fetch declarations only for those specific valid situations
            var allRelevantDeclarations = await _context.DeclarationsStrategiques
                .Include(d => d.IndicateurStrategique).ThenInclude(i => i.CategorieIndicateur)
                .Include(d => d.Situation)
                .Where(d => latestSituationIds.Contains(d.IDSituation))
                .ToListAsync();

            var declarationsByYear = allRelevantDeclarations.GroupBy(d => d.Situation.Year).OrderByDescending(g => g.Key);

            foreach (var yearGroup in declarationsByYear)
            {
                if (!int.TryParse(yearGroup.Key, out int currentYear)) continue;
                var yearSummary = new YearlySummaryViewModel { Year = currentYear };

                var declarationsByCategory = yearGroup.GroupBy(d => d.IndicateurStrategique.CategorieIndicateur);
                foreach (var categoryGroup in declarationsByCategory.OrderBy(g => g.Key.IdCategIn))
                {
                    var chart = new DashboardChartViewModel
                    {
                        CategoryName = $"{categoryGroup.Key.IntituleCategIn} (Dernière Situation Validée de {currentYear})",
                        ChartId = $"chart_{yearSummary.Year}_{categoryGroup.Key.IdCategIn}"
                    };

                    foreach (var declaration in categoryGroup.OrderBy(d => d.IndicateurStrategique.IdIndic))
                    {
                        chart.Labels.Add(declaration.IndicateurStrategique.IntituleIn);
                        double taux = declaration.taux ?? 0;
                        double cible = declaration.Cible ?? 0;

                        chart.PerformanceJusquaCible.Add(Math.Min(taux, cible));
                        chart.EcartNegatif.Add(Math.Max(0, cible - taux));
                        chart.DepassementCible.Add(Math.Max(0, taux - cible));
                        chart.TauxAtteintData.Add(taux);
                        chart.CibleData.Add(cible);
                    }
                    yearSummary.Charts.Add(chart);
                }
                viewModel.YearlySummaries.Add(yearSummary);
            }

            return View(viewModel);
        }

        // --- LIST and CREATE SITUATIONS ---
        public async Task<IActionResult> _Editer()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userCodeDiw = User.FindFirstValue("CodeDIW");
            var situations = await _context.Situations
                                     .Where(s => s.DIW == userCodeDiw)
                                     .OrderByDescending(s => s.EditDate ?? s.CreateDate)
                                     .ToListAsync();
            ViewBag.CurrentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var usersInDiw = await _context.Users
                .Where(u => u.CodeDIW == userCodeDiw)
                .ToDictionaryAsync(u => u.ID_User, u => $"{u.FirstNmUser} {u.LastNmUser}");
            ViewBag.DiwUsers = usersInDiw;
            return View(new IndexViewModel { Situations = situations });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> _Editer(string Month, string Year)
        {
            var userCodeDiw = User.FindFirstValue("CodeDIW");
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(Month) || string.IsNullOrEmpty(Year))
            {
                TempData["Message"] = "Veuillez sélectionner un mois et une année.";
                return RedirectToAction(nameof(_Editer));
            }

            if (await _context.Situations.AnyAsync(s => s.DIW == userCodeDiw && s.Month == Month && s.Year == Year))
            {
                TempData["Message"] = "Une situation pour cette période existe déjà.";
                return RedirectToAction(nameof(_Editer));
            }

            var newSituation = new Situation
            {
                IDSituation = Guid.NewGuid().ToString(),
                Month = Month,
                Year = Year,
                DIW = userCodeDiw,
                User_id = userId,
                CreateDate = DateTime.Now,
                Statut = 0
            };

            _context.Situations.Add(newSituation);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(_Editer));
        }

        // --- SAVE DRAFT ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnregistrerBrouillon(string id, [FromForm] Dictionary<string, IndicatorInputModel> indicators)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var situation = await _context.Situations.FirstOrDefaultAsync(s => s.IDSituation == id && s.User_id == userId);
            if (situation == null) return Forbid();

            var oldDrafts = _context.DeclarationsStrategiquesDrafts.Where(d => d.IDSituation == id);
            _context.DeclarationsStrategiquesDrafts.RemoveRange(oldDrafts);

            foreach (var indicator in indicators.Values)
            {
                float numerateur = indicator.Numerateur ?? 0;
                float denominateur = indicator.Denominateur ?? 0;
                float cible = (float)indicator.cible;
                float taux = (denominateur != 0) ? (numerateur / denominateur) * 100 : 0;
                float ecart = taux - cible;

                var draft = new DeclarationStrategiqueDraft
                {
                    IDSituation = id,
                    IdIndic = indicator.IndicatorId,
                    Numerateur = numerateur,
                    Denominateur = denominateur,
                    Cible = cible,
                    Taux = taux,
                    Ecart = ecart
                };
                _context.DeclarationsStrategiquesDrafts.Add(draft);
            }

            situation.EditDate = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Brouillon enregistré avec succès.";
            return RedirectToAction(nameof(_Editer));
        }

        // --- CONFIRM AND SUBMIT ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirmer(string id, [FromForm] Dictionary<string, IndicatorInputModel> indicators)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var situation = await _context.Situations.FirstOrDefaultAsync(s => s.IDSituation == id && s.User_id == userId);
            if (situation == null) return Forbid();

            _context.DeclarationsStrategiques.RemoveRange(_context.DeclarationsStrategiques.Where(d => d.IDSituation == id));
            _context.DeclarationsStrategiquesDrafts.RemoveRange(_context.DeclarationsStrategiquesDrafts.Where(d => d.IDSituation == id));

            foreach (var indicator in indicators.Values)
            {
                float numerateur = indicator.Numerateur ?? 0;
                float denominateur = indicator.Denominateur ?? 0;
                float cible = (float)indicator.cible;
                float taux = (denominateur != 0) ? (numerateur / denominateur) * 100 : 0;
                float ecart = taux - cible;

                var declaration = new DeclarationStrategique
                {
                    IDSituation = id,
                    IdIndic = indicator.IndicatorId,
                    Numerateur = numerateur,
                    Denominateur = denominateur,
                    Cible = cible,
                    taux = taux,
                    ecart = ecart
                };
                _context.DeclarationsStrategiques.Add(declaration);
            }

            situation.Statut = 1; 
            situation.ConfirmDate = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "La situation a été validée avec succès.";
            return RedirectToAction(nameof(_Editer));
        }


        private int GetMonthNumber(string monthName)
        {
            if (string.IsNullOrEmpty(monthName)) return 0;
            return DateTime.ParseExact(monthName, "MMMM", new System.Globalization.CultureInfo("fr-FR")).Month;
        }
        public async Task<IActionResult> Delete(string? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var situation = await _context.Situations.FirstOrDefaultAsync(s => s.IDSituation == id && s.User_id == userId);

            if (situation == null) return Forbid();
            return View(situation);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(String id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var situation = await _context.Situations.FirstOrDefaultAsync(s => s.IDSituation == id && s.User_id == userId);

            if (situation == null) return NotFound();

            _context.Situations.Remove(situation);
            situation.DeleteDate = DateTime.Now;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(_Editer));
        }


        public async Task<IActionResult> LogOut()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Access");
        }

        public async Task<IActionResult> ExportToExcel(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var situation = await _context.Situations.FirstOrDefaultAsync(s => s.IDSituation == id && s.User_id == userId);
            if (situation == null) return Forbid();

            var declarations = await _context.DeclarationsStrategiques
                .Include(d => d.IndicateurStrategique).ThenInclude(i => i.Objectif)
                .Include(d => d.IndicateurStrategique).ThenInclude(i => i.CategorieIndicateur)
                .Where(d => d.IDSituation == id)
                .OrderBy(d => d.IndicateurStrategique.CategorieIndicateur.IdCategIn)
                .ThenBy(d => d.IndicateurStrategique.Objectif.idobj)
                .ThenBy(d => d.IndicateurStrategique.IdIndic)
                .ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Situation Stratégique");
                worksheet.Cell("A1").Value = $"Situation Stratégique - {situation.Month} {situation.Year}";
                worksheet.Cell("A1").Style.Font.Bold = true;
                worksheet.Cell("A1").Style.Font.FontSize = 14;
                worksheet.Range("A1:F1").Merge();

                string[] headers = { "Indicateur", "Numérateur", "Dénominateur", "Taux (%)", "Cible (%)", "Écart (%)" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(3, i + 1).Value = headers[i];
                    worksheet.Cell(3, i + 1).Style.Font.Bold = true;
                }

                int currentRow = 4;
                string currentCategory = null;
                string currentObjective = null;

                foreach (var dec in declarations)
                {
                    var indicateur = dec.IndicateurStrategique;
                    var categorie = indicateur.CategorieIndicateur;
                    var objectifName = indicateur.Objectif?.Intituleobj ?? "Indicateurs Généraux";

                    if (categorie.IntituleCategIn != currentCategory)
                    {
                        currentCategory = categorie.IntituleCategIn;
                        worksheet.Cell(currentRow, 1).Value = currentCategory;
                        worksheet.Range(currentRow, 1, currentRow, 6).Merge().Style.Fill.BackgroundColor = XLColor.Gray;
                        worksheet.Cell(currentRow, 1).Style.Font.FontColor = XLColor.White;
                        worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                        currentRow++;
                        currentObjective = null;
                    }

                    if (objectifName != currentObjective)
                    {
                        currentObjective = objectifName;
                        worksheet.Cell(currentRow, 1).Value = $"Objectif: {currentObjective}";
                        worksheet.Range(currentRow, 1, currentRow, 6).Merge().Style.Fill.BackgroundColor = XLColor.LightGray;
                        worksheet.Cell(currentRow, 1).Style.Font.Italic = true;
                        worksheet.Cell(currentRow, 1).Style.Alignment.SetIndent(1);
                        currentRow++;
                    }

                    worksheet.Cell(currentRow, 1).Value = indicateur.IntituleIn;
                    worksheet.Cell(currentRow, 1).Style.Alignment.SetIndent(2);
                    worksheet.Cell(currentRow, 2).Value = dec.Numerateur;
                    worksheet.Cell(currentRow, 3).Value = dec.Denominateur;
                    worksheet.Cell(currentRow, 4).Value = dec.taux;
                    worksheet.Cell(currentRow, 5).Value = dec.Cible;
                    worksheet.Cell(currentRow, 6).Value = dec.ecart;
                    currentRow++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Situation_Strategique_{situation.Month}_{situation.Year}.xlsx");
                }
            }
        }

        public async Task<IActionResult> ExportToPdf(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var situation = await _context.Situations.FirstOrDefaultAsync(s => s.IDSituation == id && s.User_id == userId);
            if (situation == null) return Forbid();

            var declarations = await _context.DeclarationsStrategiques
                .Include(d => d.IndicateurStrategique).ThenInclude(i => i.Objectif)
                .Include(d => d.IndicateurStrategique).ThenInclude(i => i.CategorieIndicateur)
                .Where(d => d.IDSituation == id)
                .OrderBy(d => d.IndicateurStrategique.CategorieIndicateur.IdCategIn)
                .ThenBy(d => d.IndicateurStrategique.Objectif.idobj)
                .ThenBy(d => d.IndicateurStrategique.IdIndic)
                .ToListAsync();

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(9));
                    page.Header().AlignCenter().Text($"Situation Stratégique - {situation.Month} {situation.Year}").Bold().FontSize(16);
                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3.5f);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                        });
                        table.Header(header =>
                        {
                            header.Cell().Text("Indicateur").Bold();
                            header.Cell().AlignCenter().Text("Numérateur").Bold();
                            header.Cell().AlignCenter().Text("Dénominateur").Bold();
                            header.Cell().AlignCenter().Text("Taux (%)").Bold();
                            header.Cell().AlignCenter().Text("Cible (%)").Bold();
                            header.Cell().AlignCenter().Text("Écart (%)").Bold();
                        });

                        string currentCategory = "";
                        string currentObjective = "";

                        foreach (var dec in declarations)
                        {
                            if (dec.IndicateurStrategique.CategorieIndicateur.IntituleCategIn != currentCategory)
                            {
                                currentCategory = dec.IndicateurStrategique.CategorieIndicateur.IntituleCategIn;
                                table.Cell()
                                .ColumnSpan(6)
                                .Background(Colors.Grey.Darken2)
                                .Padding(4)
                                .Text(text =>
                                {
                                    text.Span(currentCategory)
                                        .FontColor(Colors.White)
                                        .Bold();
                                });
                                currentObjective = "";
                            }

                            if (dec.IndicateurStrategique.Objectif.Intituleobj != currentObjective)
                            {
                                currentObjective = dec.IndicateurStrategique.Objectif.Intituleobj;
                                table.Cell().ColumnSpan(6).Background(Colors.Grey.Lighten3).Padding(2).PaddingLeft(8).Text($"Objectif: {currentObjective}").Italic();
                            }

                            table.Cell().PaddingLeft(16).Text(dec.IndicateurStrategique.IntituleIn);
                            table.Cell().AlignCenter().Text($"{dec.Numerateur}");
                            table.Cell().AlignCenter().Text($"{dec.Denominateur}");
                            table.Cell().AlignCenter().Text($"{dec.taux:F2}");
                            table.Cell().AlignCenter().Text($"{dec.Cible:F2}");
                            table.Cell().AlignCenter().Text($"{dec.ecart:F2}");
                        }
                    });
                    page.Footer().AlignCenter().Text(x => { x.Span("Page "); x.CurrentPageNumber(); });
                });
            }).GeneratePdf();

            return File(pdf, "application/pdf", $"Situation_Strategique_{situation.Month}_{situation.Year}.pdf");
        }
    }
}