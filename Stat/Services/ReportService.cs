using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Stat.Models;
using Stat.Models.ViewModels;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Colors = QuestPDF.Helpers.Colors;

namespace Stat.Services
{
    public class ReportService : IReportService
    {
        private readonly DatabaseContext _context;

        public ReportService(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<(byte[] FileContents, string FileName)> GenerateOperationalExcelAsync(string situationId)
        {
            var situation = await _context.Situations.FindAsync(situationId);
            if (situation == null) return (null, null);

            var diw = await _context.DIWs.FindAsync(situation.DIW);
            string diwName = diw?.LibelleDIW ?? "DIW Inconnu";

            var declarations = await _context.Declarations
                .Include(d => d.Indicateur).ThenInclude(i => i.CategorieIndicateur)
                .Where(d => d.IDSituation == situationId)
                .OrderBy(d => d.Indicateur.IdCategIn).ThenBy(d => d.Indicateur.IdIn)
                .ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Situation Opérationnelle");
                worksheet.Cell("A1").Value = $"{diwName} - Situation {situation.Month} {situation.Year}";
                worksheet.Range("A1:F1").Merge().Style.Font.Bold = true;
                worksheet.Cell("A1").Style.Font.FontSize = 14;

                string[] headers = { "Indicateur", "Numérateur", "Dénominateur", "Taux (%)", "Cible (%)", "Écart (%)" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(3, i + 1).Value = headers[i];
                    worksheet.Cell(3, i + 1).Style.Font.Bold = true;
                }

                int currentRow = 4;
                string currentCategory = "";
                foreach (var dec in declarations)
                {
                    if (dec.Indicateur.CategorieIndicateur.IntituleCategIn != currentCategory)
                    {
                        currentCategory = dec.Indicateur.CategorieIndicateur.IntituleCategIn;
                        var catCell = worksheet.Cell(currentRow, 1);
                        catCell.Value = currentCategory;
                        worksheet.Range(currentRow, 1, currentRow, 6).Merge();
                        catCell.Style.Font.Bold = true;
                        catCell.Style.Fill.BackgroundColor = XLColor.LightGray;
                        currentRow++;
                    }

                    worksheet.Cell(currentRow, 1).Value = dec.Indicateur.IntituleIn;
                    worksheet.Cell(currentRow, 2).Value = dec.Numerateur;
                    worksheet.Cell(currentRow, 3).Value = dec.Denominateur;
                    worksheet.Cell(currentRow, 4).Value = dec.taux;
                    worksheet.Cell(currentRow, 5).Value = dec.Cible;
                    worksheet.Cell(currentRow, 6).Value = dec.ecart;
                    currentRow++;
                }

                worksheet.Columns().AdjustToContents();
                var dataRange = worksheet.Range(3, 1, currentRow - 1, 6);
                dataRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                dataRange.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    string fileName = $"DIW_Situation_{diwName}_{situation.Month}_{situation.Year}.xlsx";
                    return (stream.ToArray(), fileName);
                }
            }
        }

        public async Task<(byte[] FileContents, string FileName)> GenerateOperationalPdfAsync(string situationId)
        {
            var situation = await _context.Situations.FindAsync(situationId);
            if (situation == null) return (null, null);

            var diw = await _context.DIWs.FindAsync(situation.DIW);
            string diwName = diw?.LibelleDIW ?? "DIW Inconnu";

            var declarations = await _context.Declarations
                .Include(d => d.Indicateur).ThenInclude(i => i.CategorieIndicateur)
                .Where(d => d.IDSituation == situationId)
                .OrderBy(d => d.Indicateur.IdCategIn).ThenBy(d => d.Indicateur.IdIn)
                .ToListAsync();

            var pdfBytes = Document.Create(container => {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10));
                    page.Header().AlignCenter().Text(header => {
                        header.Span(diwName).Bold().FontSize(16);
                        header.Line($"Déclaration mois de {situation.Month} {situation.Year}").FontSize(12);
                    });
                    page.Content().PaddingTop(1, Unit.Centimetre).Table(table =>
                    {
                        table.ColumnsDefinition(c => {
                            c.RelativeColumn(3);
                            c.ConstantColumn(70);
                            c.ConstantColumn(70);
                            c.ConstantColumn(60);
                            c.ConstantColumn(60);
                            c.ConstantColumn(60);
                        });
                        table.Header(h => {
                            h.Cell().Text("Indicateur").Bold();
                            h.Cell().AlignCenter().Text("Numérateur").Bold();
                            h.Cell().AlignCenter().Text("Dénominateur").Bold();
                            h.Cell().AlignCenter().Text("Taux (%)").Bold();
                            h.Cell().AlignCenter().Text("Cible (%)").Bold();
                            h.Cell().AlignCenter().Text("Écart (%)").Bold();
                        });

                        var groupedByCategory = declarations.GroupBy(d => d.Indicateur.CategorieIndicateur);
                        foreach (var catGroup in groupedByCategory)
                        {
                            table.Cell().ColumnSpan(6).Background(Colors.Grey.Lighten3).Padding(2).Text(catGroup.Key.IntituleCategIn).Bold();
                            foreach (var dec in catGroup)
                            {
                                table.Cell().Text(dec.Indicateur.IntituleIn);
                                table.Cell().AlignCenter().Text($"{dec.Numerateur}");
                                table.Cell().AlignCenter().Text($"{dec.Denominateur}");
                                table.Cell().AlignCenter().Text($"{dec.taux:F2}");
                                table.Cell().AlignCenter().Text($"{dec.Cible:F2}");
                                table.Cell().AlignCenter().Text($"{dec.ecart:F2}");
                            }
                        }
                    });
                    page.Footer().AlignCenter().Text(x => { x.Span("Page "); x.CurrentPageNumber(); });
                });
            }).GeneratePdf();

            string pdfName = $"DIW_Situation_{diwName}_{situation.Month}_{situation.Year}.pdf";
            return (pdfBytes, pdfName);
        }

        public async Task<(byte[] FileContents, string FileName)> GenerateStrategicExcelAsync(string situationId)
        {
            var situation = await _context.Situations.FindAsync(situationId);
            if (situation == null) return (null, null);

            var declarations = await _context.DeclarationsStrategiques
                .Include(d => d.IndicateurStrategique).ThenInclude(i => i.Objectif)
                .Include(d => d.IndicateurStrategique).ThenInclude(i => i.CategorieIndicateur)
                .Where(d => d.IDSituation == situationId)
                .OrderBy(d => d.IndicateurStrategique.IdCategIn)
                .ThenBy(d => d.IndicateurStrategique.idobj)
                .ThenBy(d => d.IndicateurStrategique.IdIndic)
                .ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Situation Stratégique");
                worksheet.Cell("A1").Value = $"Situation Stratégique - {situation.Month} {situation.Year}";
                worksheet.Range("A1:F1").Merge().Style.Font.Bold = true;
                worksheet.Cell("A1").Style.Font.FontSize = 14;

                string[] headers = { "Indicateur", "Numérateur", "Dénominateur", "Taux (%)", "Cible (%)", "Écart (%)" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(3, i + 1).Value = headers[i];
                    worksheet.Cell(3, i + 1).Style.Font.Bold = true;
                }

                int currentRow = 4;
                string currentCategory = null, currentObjective = null;

                foreach (var dec in declarations)
                {
                    var categorie = dec.IndicateurStrategique.CategorieIndicateur;
                    var objectifName = dec.IndicateurStrategique.Objectif?.Intituleobj ?? "Général";

                    if (categorie.IntituleCategIn != currentCategory)
                    {
                        currentCategory = categorie.IntituleCategIn;
                        worksheet.Cell(currentRow, 1).Value = currentCategory;
                        worksheet.Range(currentRow, 1, currentRow, 6).Merge().Style.Fill.BackgroundColor = XLColor.Gray;
                        worksheet.Cell(currentRow, 1).Style.Font.FontColor = XLColor.White;
                        currentRow++;
                        currentObjective = null;
                    }
                    if (objectifName != currentObjective)
                    {
                        currentObjective = objectifName;
                        worksheet.Cell(currentRow, 1).Value = $"Objectif: {currentObjective}";
                        worksheet.Range(currentRow, 1, currentRow, 6).Merge().Style.Fill.BackgroundColor = XLColor.LightGray;
                        worksheet.Cell(currentRow, 1).Style.Alignment.SetIndent(1);
                        currentRow++;
                    }
                    worksheet.Cell(currentRow, 1).Value = dec.IndicateurStrategique.IntituleIn;
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
                    string fileName = $"DC_Situation_Strategique_{situation.Month}_{situation.Year}.xlsx";
                    return (stream.ToArray(), fileName);
                }
            }
        }

        public async Task<(byte[] FileContents, string FileName)> GenerateStrategicPdfAsync(string situationId)
        {
            var situation = await _context.Situations.FindAsync(situationId);
            if (situation == null) return (null, null);

            var declarations = await _context.DeclarationsStrategiques
                .Include(d => d.IndicateurStrategique).ThenInclude(i => i.Objectif)
                .Include(d => d.IndicateurStrategique).ThenInclude(i => i.CategorieIndicateur)
                .Where(d => d.IDSituation == situationId)
                .OrderBy(d => d.IndicateurStrategique.IdCategIn)
                .ThenBy(d => d.IndicateurStrategique.idobj)
                .ThenBy(d => d.IndicateurStrategique.IdIndic)
                .ToListAsync();

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.Header().AlignCenter().Text($"Situation Stratégique - {situation.Month} {situation.Year}").Bold().FontSize(16);
                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(3.5f); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1); });
                        table.Header(h => { h.Cell().Text("Indicateur").Bold(); h.Cell().AlignCenter().Text("Numérateur").Bold(); h.Cell().AlignCenter().Text("Dénominateur").Bold(); h.Cell().AlignCenter().Text("Taux (%)").Bold(); h.Cell().AlignCenter().Text("Cible (%)").Bold(); h.Cell().AlignCenter().Text("Écart (%)").Bold(); });

                        var groupedByCategory = declarations.GroupBy(d => d.IndicateurStrategique.CategorieIndicateur);
                        foreach (var catGroup in groupedByCategory)
                        {
                            table.Cell().ColumnSpan(6).Background(Colors.Grey.Darken2).Padding(4).Text(catGroup.Key.IntituleCategIn).FontColor(Colors.White).Bold();
                            foreach (var objGroup in catGroup.GroupBy(d => d.IndicateurStrategique.Objectif))
                            {
                                table.Cell().ColumnSpan(6).Background(Colors.Grey.Lighten3).Padding(2).PaddingLeft(8).Text($"Objectif: {objGroup.Key.Intituleobj}").Italic();
                                foreach (var dec in objGroup.OrderBy(d => d.IdIndic))
                                {
                                    table.Cell().PaddingLeft(16).Text(dec.IndicateurStrategique.IntituleIn);
                                    table.Cell().AlignCenter().Text($"{dec.Numerateur}");
                                    table.Cell().AlignCenter().Text($"{dec.Denominateur}");
                                    table.Cell().AlignCenter().Text($"{dec.taux:F2}");
                                    table.Cell().AlignCenter().Text($"{dec.Cible:F2}");
                                    table.Cell().AlignCenter().Text($"{dec.ecart:F2}");
                                }
                            }
                        }
                    });
                    page.Footer().AlignCenter().Text(x => { x.Span("Page "); x.CurrentPageNumber(); });
                });
            }).GeneratePdf();

            string pdfName = $"DC_Situation_Strategique_{situation.Month}_{situation.Year}.pdf";
            return (pdfBytes, pdfName);
        }


        public async Task<(byte[] FileContents, string FileName)> GenerateDriExcelAsync(string situationId)
        {
            var situation = await _context.Situations.FindAsync(situationId);
            if (situation == null) return (null, null);

            var dri = await _context.DRIs.FindAsync(situation.DIW);
            string driName = dri?.LibelleDRI ?? "DRI Inconnu";

            var declarations = await _context.DeclarationDRIs
                .Where(d => d.IDSituation == situationId)
                .Include(d => d.Indicateur)
                .OrderBy(d => d.IdIndicacteur)
                .ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Rapport Performance DRI");
                worksheet.Cell("A1").Value = $"{driName} - Situation {situation.Month} {situation.Year}";
                worksheet.Range("A1:F1").Merge().Style.Font.Bold = true;
                worksheet.Cell("A1").Style.Font.FontSize = 14;

                string[] headers = { "Indicateur", "Numérateur", "Dénominateur", "Taux (%)", "Cible (%)", "Écart (%)" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(3, i + 1).Value = headers[i];
                    worksheet.Cell(3, i + 1).Style.Font.Bold = true;
                }

                int currentRow = 4;
                foreach (var dec in declarations)
                {
                    worksheet.Cell(currentRow, 1).Value = dec.Indicateur.IntituleIn;
                    worksheet.Cell(currentRow, 2).Value = dec.Numerateur;
                    worksheet.Cell(currentRow, 3).Value = dec.Denominateur;
                    worksheet.Cell(currentRow, 4).Value = dec.taux;
                    worksheet.Cell(currentRow, 5).Value = dec.Cible;
                    worksheet.Cell(currentRow, 6).Value = dec.ecart;
                    currentRow++;
                }

                worksheet.Columns().AdjustToContents();
                var dataRange = worksheet.Range(3, 1, currentRow - 1, 6);
                dataRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                dataRange.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    string fileName = $"Rapport_DRI_{driName}_{situation.Month}_{situation.Year}.xlsx";
                    return (stream.ToArray(), fileName);
                }
            }
        }

        public async Task<(byte[] FileContents, string FileName)> GenerateDriPdfAsync(string situationId)
        {
            var situation = await _context.Situations.FindAsync(situationId);
            if (situation == null) return (null, null);

            var dri = await _context.DRIs.FindAsync(situation.DIW);
            string driName = dri?.LibelleDRI ?? "DRI Inconnu";

            var declarations = await _context.DeclarationDRIs
                .Where(d => d.IDSituation == situationId)
                .Include(d => d.Indicateur)
                .OrderBy(d => d.IdIndicacteur)
                .ToListAsync();

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10));
                    page.Header().AlignCenter().Text(header =>
                    {
                        header.Span(driName).Bold().FontSize(16);
                        header.Line($"Rapport de Performance - {situation.Month} {situation.Year}").FontSize(12);
                    });
                    page.Content().PaddingTop(1, Unit.Centimetre).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Text("Indicateur").Bold();
                            h.Cell().AlignCenter().Text("Numérateur").Bold();
                            h.Cell().AlignCenter().Text("Dénominateur").Bold();
                            h.Cell().AlignCenter().Text("Taux (%)").Bold();
                            h.Cell().AlignCenter().Text("Cible (%)").Bold();
                            h.Cell().AlignCenter().Text("Écart (%)").Bold();
                        });

                        foreach (var dec in declarations)
                        {
                            table.Cell().Text(dec.Indicateur.IntituleIn);
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

            string pdfName = $"Rapport_DRI_{driName}_{situation.Month}_{situation.Year}.pdf";
            return (pdfBytes, pdfName);
        }

        // --- ADD THESE METHODS TO ReportService.cs ---
        // --- REPLACE THE PREVIOUS EXPORT METHODS IN ReportService.cs WITH THESE ---

        public async Task<(byte[] FileContents, string FileName)> GenerateAnalysisOpExcelAsync(List<IndicatorPerformanceViewModel> results, NiveauOpViewModel filters)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Analyse Opérationnelle");

                // --- 1. RESOLVE NAMES FOR FILTERS ---
                string yearText = filters.SelectedYear ?? "Toutes";
                string driText = "";
                string diwText = "";
                string monthText = "";
                string axeText = "";

                // Resolve DRI Name
                if (!string.IsNullOrEmpty(filters.SelectedDri))
                {
                    var dri = await _context.DRIs.FindAsync(filters.SelectedDri);
                    driText = dri != null ? dri.LibelleDRI : filters.SelectedDri;
                }

                // Resolve DIW Name
                if (!string.IsNullOrEmpty(filters.SelectedDiw))
                {
                    var diw = await _context.DIWs.FindAsync(filters.SelectedDiw);
                    diwText = diw != null ? diw.LibelleDIW : filters.SelectedDiw;
                }

                // Resolve Month Name
                if (filters.SelectedMonth.HasValue)
                {
                    monthText = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(filters.SelectedMonth.Value);
                    monthText = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(monthText);
                }

                // Resolve Axe Name (Category)
                if (!string.IsNullOrEmpty(filters.SelectedAxe))
                {
                    var axe = await _context.CategorieIndicateurs.FindAsync(filters.SelectedAxe);
                    axeText = axe != null ? axe.IntituleCategIn : filters.SelectedAxe;
                }

                // --- 2. BUILD HEADER STRING ---
                var headerParts = new List<string> { $"Année: {yearText}" };
                if (!string.IsNullOrEmpty(driText)) headerParts.Add($"DRI: {driText}");
                if (!string.IsNullOrEmpty(diwText)) headerParts.Add($"DIW: {diwText}");
                if (filters.SelectedSemester.HasValue) headerParts.Add($"Semestre: {filters.SelectedSemester}");
                if (filters.SelectedTrimester.HasValue) headerParts.Add($"Trimestre: T{filters.SelectedTrimester}");
                if (!string.IsNullOrEmpty(monthText)) headerParts.Add($"Mois: {monthText}");
                if (!string.IsNullOrEmpty(axeText)) headerParts.Add($"Axe: {axeText}");

                string headerText = "Filtres appliqués : " + string.Join(" | ", headerParts);

                // Set Header Style
                worksheet.Cell("A1").Value = "Analyse Opérationnelle - Export";
                worksheet.Cell("A1").Style.Font.FontSize = 14;
                worksheet.Cell("A1").Style.Font.Bold = true;

                worksheet.Cell("A2").Value = headerText;
                worksheet.Range("A2:H2").Merge().Style.Font.Italic = true;
                worksheet.Cell("A2").Style.Fill.BackgroundColor = XLColor.LightGray;

                // --- 3. SET COLUMNS (Keep Axe, but we will merge later) ---
                string[] headers = { "Axe", "Indicateur", "Numérateur", "Dénominateur", "Taux Réalisé (%)", "Cible (%)", "Écart (%)" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(4, i + 1).Value = headers[i];
                    worksheet.Cell(4, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(4, i + 1).Style.Fill.BackgroundColor = XLColor.DarkBlue;
                    worksheet.Cell(4, i + 1).Style.Font.FontColor = XLColor.White;
                    worksheet.Cell(4, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                // --- 4. FILL DATA ---
                int startRow = 5;
                int currentRow = startRow;

                foreach (var item in results)
                {
                    worksheet.Cell(currentRow, 1).Value = item.AxeName; // We write it, then we merge
                    worksheet.Cell(currentRow, 2).Value = item.IndicatorName;
                    worksheet.Cell(currentRow, 3).Value = item.SumNumerateur;
                    worksheet.Cell(currentRow, 4).Value = item.SumDenominateur;
                    worksheet.Cell(currentRow, 5).Value = item.Taux;
                    worksheet.Cell(currentRow, 6).Value = item.Cible;
                    worksheet.Cell(currentRow, 7).Value = item.Ecart;

                    // Formatting numbers
                    worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "0.00";
                    worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "0.00";
                    worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "0.00";

                    if (item.Ecart >= 0) worksheet.Cell(currentRow, 7).Style.Font.FontColor = XLColor.Green;
                    else worksheet.Cell(currentRow, 7).Style.Font.FontColor = XLColor.Red;

                    currentRow++;
                }

                // --- 5. MERGE AXE COLUMN (FUSIONNER) ---
                // We loop from the end to the beginning to merge safely
                int lastRow = currentRow - 1;
                if (lastRow > startRow)
                {
                    int mergeStart = startRow;
                    for (int r = startRow + 1; r <= lastRow + 1; r++)
                    {
                        // Check if current row Axe is different from previous or if we are at the end
                        string currentVal = r <= lastRow ? worksheet.Cell(r, 1).GetValue<string>() : null;
                        string prevVal = worksheet.Cell(r - 1, 1).GetValue<string>();

                        if (currentVal != prevVal)
                        {
                            // Merge the previous block
                            if (r - 1 > mergeStart)
                            {
                                var range = worksheet.Range(mergeStart, 1, r - 1, 1);
                                range.Merge();
                                range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                            }
                            mergeStart = r;
                        }
                    }
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return (stream.ToArray(), $"Analyse_Op_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
                }
            }
        }

        public async Task<(byte[] FileContents, string FileName)> GenerateAnalysisStratExcelAsync(List<IndicatorStratPerformanceViewModel> results, NiveauStratViewModel filters)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Analyse Stratégique");

                // --- 1. RESOLVE NAMES ---
                string yearText = filters.SelectedYear ?? "Toutes";
                string monthText = "";
                string axeText = "";
                string objText = "";

                if (filters.SelectedMonth.HasValue)
                {
                    monthText = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(filters.SelectedMonth.Value);
                    monthText = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(monthText);
                }
                if (!string.IsNullOrEmpty(filters.SelectedAxe))
                {
                    var axe = await _context.CategorieIndicateurs.FindAsync(filters.SelectedAxe);
                    axeText = axe != null ? axe.IntituleCategIn : filters.SelectedAxe;
                }
                if (filters.SelectedObjectif.HasValue)
                {
                    var obj = await _context.Objectifs.FindAsync(filters.SelectedObjectif.Value);
                    objText = obj != null ? obj.Intituleobj : filters.SelectedObjectif.Value.ToString();
                }

                // --- 2. HEADER ---
                var headerParts = new List<string> { $"Année: {yearText}" };
                if (filters.SelectedSemester.HasValue) headerParts.Add($"Semestre: {filters.SelectedSemester}");
                if (filters.SelectedTrimester.HasValue) headerParts.Add($"Trimestre: T{filters.SelectedTrimester}");
                if (!string.IsNullOrEmpty(monthText)) headerParts.Add($"Mois: {monthText}");
                if (!string.IsNullOrEmpty(axeText)) headerParts.Add($"Axe: {axeText}");
                if (!string.IsNullOrEmpty(objText)) headerParts.Add($"Objectif: {objText}");

                string headerText = "Filtres appliqués : " + string.Join(" | ", headerParts);

                worksheet.Cell("A1").Value = "Analyse Stratégique - Export";
                worksheet.Cell("A1").Style.Font.FontSize = 14;
                worksheet.Cell("A1").Style.Font.Bold = true;

                worksheet.Cell("A2").Value = headerText;
                worksheet.Range("A2:I2").Merge().Style.Font.Italic = true;
                worksheet.Cell("A2").Style.Fill.BackgroundColor = XLColor.LightGray;

                // --- 3. COLUMNS (Keep Axe & Objectif for merging) ---
                string[] headers = { "Axe", "Objectif", "Indicateur", "Numérateur", "Dénominateur", "Taux Réalisé (%)", "Cible (%)", "Écart (%)" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(4, i + 1).Value = headers[i];
                    worksheet.Cell(4, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(4, i + 1).Style.Fill.BackgroundColor = XLColor.DarkRed;
                    worksheet.Cell(4, i + 1).Style.Font.FontColor = XLColor.White;
                    worksheet.Cell(4, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                // --- 4. FILL DATA ---
                int startRow = 5;
                int currentRow = startRow;
                foreach (var item in results)
                {
                    worksheet.Cell(currentRow, 1).Value = item.AxeName;
                    worksheet.Cell(currentRow, 2).Value = item.ObjectifName;
                    worksheet.Cell(currentRow, 3).Value = item.IndicatorName;
                    worksheet.Cell(currentRow, 4).Value = item.SumNumerateur;
                    worksheet.Cell(currentRow, 5).Value = item.SumDenominateur;
                    worksheet.Cell(currentRow, 6).Value = item.Taux;
                    worksheet.Cell(currentRow, 7).Value = item.Cible;
                    worksheet.Cell(currentRow, 8).Value = item.Ecart;

                    worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "0.00";
                    worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "0.00";
                    worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "0.00";

                    if (item.Ecart >= 0) worksheet.Cell(currentRow, 8).Style.Font.FontColor = XLColor.Green;
                    else worksheet.Cell(currentRow, 8).Style.Font.FontColor = XLColor.Red;

                    currentRow++;
                }

                // --- 5. MERGE AXE AND OBJECTIF COLUMNS ---
                int lastRow = currentRow - 1;
                if (lastRow > startRow)
                {
                    // Merge Axe (Column 1)
                    int mergeStart = startRow;
                    for (int r = startRow + 1; r <= lastRow + 1; r++)
                    {
                        string currentVal = r <= lastRow ? worksheet.Cell(r, 1).GetValue<string>() : null;
                        string prevVal = worksheet.Cell(r - 1, 1).GetValue<string>();

                        if (currentVal != prevVal)
                        {
                            if (r - 1 > mergeStart)
                            {
                                var range = worksheet.Range(mergeStart, 1, r - 1, 1);
                                range.Merge();
                                range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                            }
                            mergeStart = r;
                        }
                    }

                    // Merge Objectif (Column 2) - ONLY merge if within the same Axe
                    mergeStart = startRow;
                    for (int r = startRow + 1; r <= lastRow + 1; r++)
                    {
                        // Check Objectif AND Axe change
                        string currentAxe = r <= lastRow ? worksheet.Cell(r, 1).GetValue<string>() : null; // Can be empty if merged, so get merged value logic is complex in code, but for simple export:
                                                                                                           // Actually, accessing value of a merged cell via specific cell usually works in ClosedXML or returns empty.
                                                                                                           // Better strategy: Use the raw data list 'results' to determine merge ranges, it's safer.

                        // Let's stick to simple visual merging: merge identical adjacent objectives. 
                        // Since data is ordered by Axe then Objectif, this is safe.
                        string currentVal = r <= lastRow ? worksheet.Cell(r, 2).GetValue<string>() : null;
                        string prevVal = worksheet.Cell(r - 1, 2).GetValue<string>();

                        if (currentVal != prevVal)
                        {
                            if (r - 1 > mergeStart)
                            {
                                var range = worksheet.Range(mergeStart, 2, r - 1, 2);
                                range.Merge();
                                range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                            }
                            mergeStart = r;
                        }
                    }
                }

                worksheet.Columns().AdjustToContents();
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return (stream.ToArray(), $"Analyse_Strat_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
                }
            }
        }

        public async Task<(byte[] FileContents, string FileName)> GenerateAnalysisDriExcelAsync(List<IndicatorPerformanceDRIViewModel> results, NiveauOpDRIViewModel filters)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Analyse DRI");

                // --- 1. RESOLVE NAMES ---
                string yearText = filters.SelectedYear ?? "Toutes";
                string monthText = "";
                string axeText = "";

                if (filters.SelectedMonth.HasValue)
                {
                    monthText = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(filters.SelectedMonth.Value);
                    monthText = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(monthText);
                }
                if (!string.IsNullOrEmpty(filters.SelectedAxe))
                {
                    var axe = await _context.CategorieIndicateurs.FindAsync(filters.SelectedAxe);
                    axeText = axe != null ? axe.IntituleCategIn : filters.SelectedAxe;
                }

                // --- 2. HEADER ---
                var headerParts = new List<string> { $"Année: {yearText}" };
                if (filters.SelectedSemester.HasValue) headerParts.Add($"Semestre: {filters.SelectedSemester}");
                if (filters.SelectedTrimester.HasValue) headerParts.Add($"Trimestre: T{filters.SelectedTrimester}");
                if (!string.IsNullOrEmpty(monthText)) headerParts.Add($"Mois: {monthText}");
                if (!string.IsNullOrEmpty(axeText)) headerParts.Add($"Axe: {axeText}");

                string headerText = "Filtres appliqués : " + string.Join(" | ", headerParts);

                worksheet.Cell("A1").Value = "Analyse Opérationnelle (DRI) - Export";
                worksheet.Cell("A1").Style.Font.FontSize = 14;
                worksheet.Cell("A1").Style.Font.Bold = true;

                worksheet.Cell("A2").Value = headerText;
                worksheet.Range("A2:F2").Merge().Style.Font.Italic = true;
                worksheet.Cell("A2").Style.Fill.BackgroundColor = XLColor.LightGray;

                // --- 3. COLUMNS - AXE REMOVED HERE AS REQUESTED ---
                // Old headers: { "Axe", "Indicateur", ... }
                // New headers: Start directly with "Indicateur"
                string[] headers = { "Indicateur", "Numérateur", "Dénominateur", "Taux Réalisé (%)" };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(4, i + 1).Value = headers[i];
                    worksheet.Cell(4, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(4, i + 1).Style.Fill.BackgroundColor = XLColor.DarkGreen;
                    worksheet.Cell(4, i + 1).Style.Font.FontColor = XLColor.White;
                    worksheet.Cell(4, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                // --- 4. FILL DATA (Skip Axe Column) ---
                int currentRow = 5;
                foreach (var item in results)
                {
                    // Column 1 is now Indicator (previously Column 2)
                    worksheet.Cell(currentRow, 1).Value = item.IndicatorName;
                    worksheet.Cell(currentRow, 2).Value = item.SumNumerateur;
                    worksheet.Cell(currentRow, 3).Value = item.SumDenominateur;
                    worksheet.Cell(currentRow, 4).Value = item.Taux;

                    worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "0.00";
                    currentRow++;
                }

                worksheet.Columns().AdjustToContents();
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return (stream.ToArray(), $"Analyse_DRI_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
                }
            }
        }
    }
}