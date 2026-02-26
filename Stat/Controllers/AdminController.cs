
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authentication; // <-- FIX: Added for SignOutAsync
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Stat.Helpers;
using Stat.Models;
using Stat.Models.ViewModels;
using System.Diagnostics; // <-- FIX: Added for Activity
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Globalization;
using Stat.Models.Enums;
using Stat.Services;

namespace Stat.Controllers
{
    //[Authorize(Roles = "Admin")] // Secure the entire controller for Admin users
    public class AdminController : Controller
    {
        private readonly DatabaseContext _context;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly IReportService _reportService;


        public AdminController(DatabaseContext context, IConfiguration config, IWebHostEnvironment hostEnvironment, IReportService reportService)
        {
            _context = context;
            _config = config;
            _hostEnvironment = hostEnvironment;
            _reportService = reportService;
        }
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index()
        {

            var hasAnyPermissions = User.HasClaim(c => c.Type == "Permission");

            // ✨ 2. If they have no permissions, redirect them directly to their profile page.
            if (!hasAnyPermissions)
            {
                return RedirectToAction("Profile");
            }
            var viewModel = new AdminDashboardViewModel();
            var today = DateTime.Now;
            var periodToCheck = today.AddMonths(-1);
            string monthName = periodToCheck.ToString("MMMM", new System.Globalization.CultureInfo("fr-FR"));
            string year = periodToCheck.Year.ToString();
            viewModel.PeriodChecked = $"{monthName} {year}";

            // --- 1. KPI CARD & USER COUNT CALCULATIONS (OPTIMIZED) ---
            var allUserQuery = _context.Users.AsQueryable();
            var allSituationsQuery = _context.Situations.AsQueryable();

            var dcUserIds = await allUserQuery.Where(u => u.Statut == (int)UserRole.DC).Select(u => u.ID_User).ToListAsync();
            var diwUserIds = await allUserQuery.Where(u => u.Statut == (int)UserRole.DIW).Select(u => u.ID_User).ToListAsync();
            var driUserIds = await allUserQuery.Where(u => u.Statut == (int)UserRole.DRI).Select(u => u.ID_User).ToListAsync();

            viewModel.TotalSituations = await allSituationsQuery.CountAsync();
            viewModel.TotalDcSituations = await allSituationsQuery.CountAsync(s => dcUserIds.Contains(s.User_id));
            viewModel.TotalDiwSituations = await allSituationsQuery.CountAsync(s => diwUserIds.Contains(s.User_id));
            viewModel.TotalDriSituations = await allSituationsQuery.CountAsync(s => driUserIds.Contains(s.User_id));


            viewModel.TotalDiwUsers = await allUserQuery.CountAsync(u => u.Statut == (int)UserRole.DIW && u.IsActive);
            viewModel.TotalDriUsers = await allUserQuery.CountAsync(u => u.Statut == (int)UserRole.DRI && u.IsActive);
            viewModel.TotalDcUsers = await allUserQuery.CountAsync(u => u.Statut == (int)UserRole.DC && u.IsActive);
            viewModel.TotalUsers = viewModel.TotalDcUsers + viewModel.TotalDriUsers+ viewModel.TotalDiwUsers;

            // --- 2. DIW PER DRI BREAKDOWN (OPTIMIZED) ---
            viewModel.DiwCountByDri = await _context.DRIs
                .OrderBy(d => d.LibelleDRI)
                .Select(d => new { d.LibelleDRI, DiwCount = d.DIWs.Count() })
                .ToDictionaryAsync(x => x.LibelleDRI, x => x.DiwCount);

            // --- 3. MISSING SITUATION ALERTS (OPTIMIZED) ---
            // CORRECTED LINE: Using .ToLower() for a translatable query
            var situationsInPeriodQuery = allSituationsQuery
                .Where(s => s.Month.ToLower() == monthName.ToLower() && s.Year == year);

            var submittedDcUserIds = await situationsInPeriodQuery
    .Where(s => dcUserIds.Contains(s.User_id))
    .Select(s => s.User_id)
    .Distinct()
    .ToListAsync();

            // ➡️ Step 2.1: Get the list of users who have missing situations
            var usersWithMissingSituations = await allUserQuery
                .Where(u => u.Statut == (int)UserRole.DC && u.IsActive && !submittedDcUserIds.Contains(u.ID_User))
                .ToListAsync();

            // ➡️ Step 2.2: Create a new list to hold just the DC names
            var missingDcNames = new List<string>();

            // ➡️ Step 2.3: Loop through the users to find their DC's name
            foreach (var user in usersWithMissingSituations)
            {
                // This is the database lookup that was causing errors in the view.
                // We now do it correctly here in the controller.
                var dc = await _context.DCs.FindAsync(user.CodeDIW);
                if (dc != null)
                {
                    missingDcNames.Add(dc.description);
                }
                else
                {
                    // Add a fallback in case a user is linked to a non-existent DC
                    missingDcNames.Add($"DC introuvable pour {user.User_name}");
                }
            }

            // ➡️ Step 2.4: Assign the final, prepared list of names to the ViewModel
            viewModel.MissingDcSituationNames = missingDcNames;

            var submittedDiwCodes = await situationsInPeriodQuery
                .Where(s => diwUserIds.Contains(s.User_id))
                .Select(s => s.DIW)
                .Distinct()
                .ToListAsync();
            var missingDiws = await _context.DIWs
                .Where(d => !submittedDiwCodes.Contains(d.CodeDIW))
                .ToListAsync();

            var allDris = await _context.DRIs.Include(d => d.DIWs).ToListAsync();
            foreach (var dri in allDris)
            {
                var missingDiwsUnderThisDri = dri.DIWs
                    .Where(diw => missingDiws.Any(missing => missing.CodeDIW == diw.CodeDIW))
                    .Select(diw => diw.LibelleDIW)
                    .ToList();

                if (missingDiwsUnderThisDri.Any())
                {
                    viewModel.MissingDriSituations.Add(new MissingDriAlertViewModel
                    {
                        DriName = dri.LibelleDRI,
                        MissingDiwNames = missingDiwsUnderThisDri
                    });
                }
            }

            // --- 4. CHART DATA CALCULATIONS ---
            viewModel.OperationalChartData = new DashboardChartDataViewModel();
            viewModel.StrategicChartData = new DashboardChartDataViewModel();

            var lastValidatedDiwSituationIds = _context.Situations
                .Where(s => s.Statut == 3 && diwUserIds.Contains(s.User_id))
                .GroupBy(s => s.DIW)
                .Select(g => g.OrderByDescending(s => s.DRIValidationDate).First().IDSituation);

            var opDeclarations = await _context.Declarations
                .Where(d => lastValidatedDiwSituationIds.Contains(d.IDSituation))
                .Include(d => d.Indicateur)
                .ToListAsync();

            var opGroups = opDeclarations.GroupBy(d => d.Indicateur);
            foreach (var group in opGroups.OrderBy(g => g.Key.IdIn))
            {
                double totalNumerateur = group.Sum(d => d.Numerateur ?? 0);
                double totalDenominateur = group.Sum(d => d.Denominateur ?? 0);
                double performance = (totalDenominateur > 0) ? (totalNumerateur / totalDenominateur) * 100 : 0;

                viewModel.OperationalChartData.Labels.Add(group.Key.IntituleIn);
                viewModel.OperationalChartData.Data.Add(performance);
            }

            var lastValidatedDcSituationIds = _context.Situations
                .Where(s => s.Statut == 3 && dcUserIds.Contains(s.User_id))
                .GroupBy(s => s.User_id)
                .Select(g => g.OrderByDescending(s => s.ConfirmDate).First().IDSituation);

            var stratDeclarations = await _context.DeclarationsStrategiques
                .Where(d => lastValidatedDcSituationIds.Contains(d.IDSituation))
                .Include(d => d.IndicateurStrategique)
                .ToListAsync();

            var stratGroups = stratDeclarations.GroupBy(d => d.IndicateurStrategique);
            foreach (var group in stratGroups.OrderBy(g => g.Key.IdIndic))
            {
                double totalNumerateur = group.Sum(d => d.Numerateur ?? 0);
                double totalDenominateur = group.Sum(d => d.Denominateur ?? 0);
                double performance = (totalDenominateur > 0) ? (totalNumerateur / totalDenominateur) * 100 : 0;

                viewModel.StrategicChartData.Labels.Add(group.Key.IntituleIn);
                viewModel.StrategicChartData.Data.Add(performance);
            }

            return View(viewModel);
        }

        [HttpGet]
        [Authorize(Policy = Permissions.ViewValidatedReports)]
        public async Task<IActionResult> ListValidatedSituations(string searchYear, string searchType, string searchStructure, int? pageNumber)
        {
            ViewData["CurrentYear"] = searchYear;
            ViewData["CurrentType"] = searchType;
            ViewData["CurrentStructure"] = searchStructure;

            // Pre-load all users and structures for efficient lookups in the loop.
            var users = await _context.Users.ToDictionaryAsync(u => u.ID_User);
            var dris = await _context.DRIs.ToDictionaryAsync(d => d.CodeDRI, d => d.LibelleDRI);
            var diws = await _context.DIWs.ToDictionaryAsync(d => d.CodeDIW, d => d.LibelleDIW);
            var dcs = await _context.DCs.ToDictionaryAsync(d => d.CodeDC, d => d.LibelleDC);

            var query = _context.Situations
                .Where(s => s.Statut == 3)
                .AsQueryable();

            // --- Filtering Logic ---
            if (!string.IsNullOrEmpty(searchYear))
            {
                query = query.Where(s => s.Year == searchYear);
            }
            if (!string.IsNullOrEmpty(searchStructure))
            {
                query = query.Where(s => s.DIW == searchStructure);
            }
            if (!string.IsNullOrEmpty(searchType))
            {
                if (searchType == "Opérationnelle")
                {
                    var userIds = users.Values.Where(u => u.Statut == 0 || u.Statut == 1).Select(u => u.ID_User);
                    query = query.Where(s => userIds.Contains(s.User_id));
                }
                else if (searchType == "Stratégique")
                {
                    var userIds = users.Values.Where(u => u.Statut == 2).Select(u => u.ID_User);
                    query = query.Where(s => userIds.Contains(s.User_id));
                }
            }

            var validatedSituations = await query
                .OrderByDescending(s => s.DRIValidationDate ?? s.ConfirmDate)
                .ToListAsync();

            // --- ViewModel Mapping with Corrected Logic ---
            var viewModelData = new List<ValidatedSituationViewModel>();
            foreach (var situation in validatedSituations)
            {
                users.TryGetValue(situation.User_id, out var user);

                string situationType = "N/A";
                string structureName = situation.DIW; // Default to the code if not found
                DateTime? validationDate = null;

                if (user != null)
                {
                    // This switch statement correctly identifies the report type based on the user's role.
                    switch ((UserRole)user.Statut)
                    {
                        case UserRole.DC: // Statut 2
                            if (dcs.TryGetValue(situation.DIW, out var dcName))
                            {
                                structureName = dcName;
                            }
                            situationType = "Stratégique";
                            validationDate = situation.ConfirmDate;
                            break;

                        case UserRole.DRI: // Statut 1
                            situationType = "Opérationnelle (DRI)";
                            // Find the DRI name from the pre-loaded dictionary.
                            if (dris.TryGetValue(situation.DIW, out var driName))
                            {
                                structureName = driName;
                            }
                            // A DRI's self-report is validated on its ConfirmDate.
                            validationDate = situation.ConfirmDate;
                            break;

                        case UserRole.DIW: // Statut 0
                            situationType = "Opérationnelle (DIW)";
                            // Find the DIW name from the pre-loaded dictionary.
                            if (diws.TryGetValue(situation.DIW, out var diwName))
                            {
                                structureName = diwName;
                            }
                            // A DIW's report is validated by a DRI on DRIValidationDate.
                            validationDate = situation.DRIValidationDate;
                            break;
                    }
                }

                viewModelData.Add(new ValidatedSituationViewModel
                {
                    SituationId = situation.IDSituation,
                    Period = $"{situation.Month} {situation.Year}",
                    StructureName = structureName,
                    SubmittedBy = user != null ? $"{user.FirstNmUser} {user.LastNmUser}" : "Utilisateur Inconnu",
                    SituationType = situationType,
                    ValidationDate = validationDate
                });
            }

            int pageSize = 15;
            var paginatedViewModel = PaginatedList<ValidatedSituationViewModel>.Create(viewModelData, pageNumber ?? 1, pageSize);

            // For filter dropdowns
            ViewBag.Years = await _context.Situations.Select(s => s.Year).Distinct().OrderByDescending(y => y).ToListAsync();
            ViewBag.Structures = await _context.DIWs.Select(d => new SelectListItem { Value = d.CodeDIW, Text = d.LibelleDIW }).ToListAsync();

            return View(paginatedViewModel);
        }

        [HttpGet]
        [Authorize(Policy = Permissions.ViewValidatedReports)]
        public async Task<IActionResult> ConsulteSituation(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var situation = await _context.Situations
                .Include(s => s.DIWNavigation)
                .FirstOrDefaultAsync(s => s.IDSituation == id);

            if (situation == null) return NotFound();

            var user = await _context.Users.FindAsync(situation.User_id);
            if (user == null) return NotFound("User associated with this situation not found.");

            ViewBag.Situation = situation;

            // --- CORRECTED LOGIC FLOW ---

            // Case 1: Check if the situation is Strategic (created by a DC user)
            if (user.Statut == (int)UserRole.DC) // Statut == 2
            {
                // Find the specific DC record using the code from the situation
                var dc = await _context.DCs.FindAsync(situation.DIW);
                // Pass its name to the view via ViewBag
                ViewBag.StructureName = dc?.LibelleDC ?? "Direction Centrale";
                var declarations = await _context.DeclarationsStrategiques
                    .Where(d => d.IDSituation == id)
                    .Include(d => d.IndicateurStrategique.CategorieIndicateur)
                    .Include(d => d.IndicateurStrategique.Objectif)
                    .ToListAsync();

                var viewModel = new AdminConsulteStratViewModel
                {
                    Situation = situation,
                    Axes = declarations
                        .GroupBy(d => d.IndicateurStrategique.CategorieIndicateur)
                        .Select(axeGroup => new AxeViewModel
                        {
                            AxeName = axeGroup.Key.IntituleCategIn,
                            Objectifs = axeGroup
                                .GroupBy(d => d.IndicateurStrategique.Objectif)
                                .Select(objGroup => new ObjectifViewModel
                                {
                                    ObjectifName = objGroup.Key.Intituleobj,
                                    Declarations = objGroup.ToList()
                                }).ToList()
                        }).ToList()
                };
                ViewBag.CurrentController = "Admin";
                return View("ConsulteStrat", viewModel);
            }

            // Case 2: Check if it's a DRI Self-Report by looking for its specific declarations
            bool isDriSelfReport = await _context.DeclarationDRIs.AnyAsync(d => d.IDSituation == id);

            if (isDriSelfReport)
            {
                // Find the specific DRI record using the code from the situation
                var dri = await _context.DRIs.FindAsync(situation.DIW);
                // Pass its name to the view via ViewBag
                ViewBag.StructureName = dri?.LibelleDRI ?? "DRI Inconnue";
                var driDeclarations = await _context.DeclarationDRIs
                    .Where(d => d.IDSituation == id)
                    .Include(d => d.Indicateur)
                    .ToListAsync();

                var viewModel = new AdminConsulteDriViewModel
                {
                    Situation = situation,
                    Declarations = driDeclarations
                };
                ViewBag.CurrentController = "Admin";
                return View("ConsulteDRI", viewModel);
            }

            // Case 3: If it's not DC or DRI Self-Report, it must be a standard DIW Operational Report
            else
            {
                var opDeclarations = await _context.Declarations
                    .Where(d => d.IDSituation == id)
                    .Include(d => d.Indicateur.CategorieIndicateur)
                    .ToListAsync();

                var opViewModel = opDeclarations
                    .GroupBy(d => d.Indicateur.CategorieIndicateur)
                    .Select(g => new CategoryIndicatorGroup
                    {
                        CategoryName = g.Key.IntituleCategIn,
                        Declarations = g.ToList()
                    }).ToList();
                ViewBag.CurrentController = "Admin";
                return View("ConsulteOp", opViewModel);
            }
        }
        
        [HttpGet]
        [Authorize(Policy = Permissions.ManageUsers)]
        public IActionResult AddUser()
        {
            return View(); // Displays the menu to choose user type
        }
        
        [HttpGet]
        [Authorize(Policy = Permissions.ManageUsers)]
        public async Task<IActionResult> AddUserForm(string userType)
        {
            var viewModel = new AddUserViewModel
            {
                UserType = userType,
                DRIOptions = await _context.DRIs
                              .OrderBy(d => d.LibelleDRI)
                              .Select(d => new SelectListItem { Value = d.CodeDRI, Text = d.LibelleDRI })
                              .ToListAsync(),
                // ✨ ADD THIS TO LOAD DC OPTIONS
                DCOptions = await _context.DCs
                             .OrderBy(d => d.LibelleDC)
                             .Select(d => new SelectListItem { Value = d.CodeDC, Text = d.LibelleDC })
                             .ToListAsync()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManageUsers)]
        public async Task<IActionResult> AddUserForm(AddUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.DRIOptions = await _context.DRIs
                    .OrderBy(d => d.LibelleDRI)
                    .Select(d => new SelectListItem { Value = d.CodeDRI, Text = d.LibelleDRI })
                    .ToListAsync();
                return View(model);
                model.DCOptions = await _context.DCs.OrderBy(d => d.LibelleDC).Select(d => new SelectListItem { Value = d.CodeDC, Text = d.LibelleDC }).ToListAsync();
                return View(model);
            }

            string userName = $"{model.FirstNmUser.Substring(0, 1).ToLower()}.{model.LastNmUser.ToLower()}";
            string codeStructure = "";
            int userRoleStatut = 0; // Default role

            // --- CORRECTED ROLE AND STATUS ASSIGNMENT ---
            switch (model.UserType)
            {
                case "DIW":
                    codeStructure = model.SelectedDIW;
                    userRoleStatut = 0;
                    break;
                case "DRI":
                    codeStructure = model.SelectedDRI;
                    userRoleStatut = 1;
                    break;
                case "DC":
                    // ✨ CHANGE THIS LINE
                    codeStructure = model.SelectedDC; // Use the selected DC code from the form
                    userRoleStatut = 2;
                    break;
                case "Admin":
                    codeStructure = "1600A00";
                    userRoleStatut = 3;
                    break;
            }
            bool userExists = await _context.Users.AnyAsync(u =>
       u.FirstNmUser == model.FirstNmUser &&
       u.LastNmUser == model.LastNmUser &&
       u.CodeDIW == codeStructure);

            if (userExists)
            {
                ModelState.AddModelError(string.Empty, "Un utilisateur avec le même nom et prénom existe déjà pour cette structure d'affectation.");

                // Recharge les options du menu déroulant avant de retourner la vue
                model.DRIOptions = await _context.DRIs
                    .OrderBy(d => d.LibelleDRI)
                    .Select(d => new SelectListItem { Value = d.CodeDRI, Text = d.LibelleDRI })
                    .ToListAsync();
                return View(model);
            }
            var plainTextPassword = PasswordHelper.GenerateRandomPassword();
            var newUser = new User
            {
                ID_User = Guid.NewGuid().ToString(),
                User_name = userName,
                FirstNmUser = model.FirstNmUser,
                LastNmUser = model.LastNmUser,
                MailUser = model.MailUser,
                TelUser = model.TelUser,
                CodeDIW = codeStructure,
                Date_deb_Affect = model.Date_deb_Affect,
                motdepasse = plainTextPassword,
                Password = sha256_hash(plainTextPassword),
                Statut = userRoleStatut,   // SETS THE CORRECT ROLE
                IsActive = false,// Inactive by default
                DateDeCreation = DateTime.Now,
                CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Utilisateur '{newUser.FirstNmUser} {newUser.LastNmUser}' créé avec succès. Mot de passe: ( {plainTextPassword} ) . IMPORTANT :Le compte est désactivé par défaut. Activation immédiate nécessaire pour la connexion.";
            return RedirectToAction("listuser");
        }
        [Authorize(Policy = Permissions.ViewUsers)]
        public async Task<IActionResult> listuser(string currentFilter, string searchString, int? pageNumber)
        {
            if (searchString != null)
            {
                pageNumber = 1;
            }
            else
            {
                searchString = currentFilter;
            }

            ViewData["CurrentFilter"] = searchString;

            // Start with the base query and sort by creation date
            var usersQuery = _context.Users
                                     .OrderByDescending(u => u.DateDeCreation)
                                     .AsQueryable();

            // Apply search filter if a search string is provided
            if (!String.IsNullOrEmpty(searchString))
            {
                usersQuery = usersQuery.Where(u => u.LastNmUser.Contains(searchString)
                                               || u.FirstNmUser.Contains(searchString)
                                               || u.User_name.Contains(searchString));
            }
            ViewBag.RoleNames = new Dictionary<int, string>
    {
        { 0, "USER_DIW" }, { 1, "USER_DRI" }, { 2, "USER_DC" }
    };
            int pageSize = 10; // Set how many users to display per page
            int currentPage = pageNumber ?? 1;
            var structureNames = new Dictionary<string, string>();

            // Charger les DIWs
            var diws = await _context.DIWs.AsNoTracking().ToListAsync();
            foreach (var diw in diws)
            {
                if (diw.CodeDIW != null) structureNames[diw.CodeDIW] = "DIW-" + diw.LibelleDIW;
            }

            // Charger les DRIs
            var dris = await _context.DRIs.AsNoTracking().ToListAsync();
            foreach (var dri in dris)
            {
                if (dri.CodeDRI != null) structureNames[dri.CodeDRI] = "DRI-" + dri.LibelleDRI;
            }

            // Charger les DCs
            var dcs = await _context.DCs.AsNoTracking().ToListAsync();
            foreach (var dc in dcs)
            {
                if (dc.CodeDC != null) structureNames[dc.CodeDC] = "DC-" + dc.LibelleDC;
            }

            // Gérer le code admin spécial (vu dans votre action AddUserForm)
            if (!structureNames.ContainsKey("1600A00"))
            {
                structureNames["1600A00"] = "Administration"; // Ou le nom que vous préférez
            }

            // Passer le dictionnaire à la vue
            ViewBag.StructureNames = structureNames;
            // Create the paginated list
            var paginatedUsers = await PaginatedList<User>.CreateAsync(usersQuery, currentPage, pageSize);
   
            return View(paginatedUsers);
        }

        // --- Note: Edit User logic would go here ---

        [HttpPost]
        [Authorize(Policy = Permissions.ManageUsers)]
        public async Task<IActionResult> ToggleIsActive(string id) // Renamed for clarity
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // This now toggles the activation status without affecting the role
            user.IsActive = !user.IsActive;

            _context.Update(user);
            await _context.SaveChangesAsync();

            // Return the new status for the UI to update
            return Ok(new { newStatus = user.IsActive });
        }

        // --- STRUCTURE MANAGEMENT (DRI/DIW) ---
        [Authorize(Policy = Permissions.ManageDRIs)]
        public async Task<IActionResult> listdri()
        {
            var dris = await _context.DRIs.ToListAsync();
            return View(dris);
        }
        [Authorize(Policy = Permissions.ManageDIWs)]
        public async Task<IActionResult> listdiw()
        {
            var diws = await _context.DIWs.Include(d => d.DRI).ToListAsync(); // Include DRI for display
            return View(diws);
        }

        // --- API for dynamic dropdowns ---
        [HttpGet]
        [Authorize(Policy = Permissions.ManageUsers)]
        public async Task<IActionResult> GetDiwsByDri(string driCode)
        {
            var diws = await _context.DIWs
                                 .Where(d => d.CodeDRI == driCode)
                                 .OrderBy(d => d.LibelleDIW)
                                 .Select(d => new { value = d.CodeDIW, text = d.LibelleDIW })
                                 .ToListAsync();
            return Json(diws);
        }

        // --- UTILITY METHODS ---

        public static String sha256_hash(String value)
        {
            StringBuilder Sb = new StringBuilder();
            using (SHA256 hash = SHA256.Create())
            {
                Encoding enc = Encoding.UTF8;
                Byte[] result = hash.ComputeHash(enc.GetBytes(value));
                foreach (Byte b in result)
                    Sb.Append(b.ToString("x2"));
            }
            return Sb.ToString();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        // In AdminController.cs

        // --- ADD/DELETE DRI ---

        [HttpGet]
        public IActionResult AddDRI()
        {
            return View(); // You will need to create a simple AddDRI.cshtml view
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDRI(DRI dri)
        {
            if (ModelState.IsValid)
            {
                _context.DRIs.Add(dri);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "DRI ajoutée avec succès.";
                return RedirectToAction("listdri");
            }
            return View(dri);
        }

        // (DeleteDRI logic is complex and will be added in a future step as requested)


        // --- ADD/DELETE DIW ---

        [HttpGet]
        public async Task<IActionResult> AddDIW()
        {
            ViewBag.DRIs = await _context.DRIs.OrderBy(d => d.LibelleDRI).ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDIW(DIW diw)
        {
            if (ModelState.IsValid)
            {
                _context.DIWs.Add(diw);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "DIW ajoutée avec succès.";
                return RedirectToAction("listdiw");
            }
            // If invalid, reload the DRI list for the dropdown
            ViewBag.DRIs = await _context.DRIs.OrderBy(d => d.LibelleDRI).ToListAsync();
            return View(diw);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDIW(string id)
        {
            var diw = await _context.DIWs.FindAsync(id);
            if (diw == null)
            {
                return NotFound();
            }

            // Check if any users are assigned to this DIW
            if (await _context.Users.AnyAsync(u => u.CodeDIW == id))
            {
                TempData["ErrorMessage"] = "Impossible de supprimer cette DIW car des utilisateurs y sont encore assignés.";
                return RedirectToAction("listdiw");
            }

            _context.DIWs.Remove(diw);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "DIW supprimée avec succès.";
            return RedirectToAction("listdiw");
        }
        // In AdminController.cs

        [HttpGet]
        public async Task<IActionResult> DeleteDRI(string id)
        {
            var driToDelete = await _context.DRIs.FindAsync(id);
            if (driToDelete == null) return NotFound();

            var viewModel = new DeleteDriViewModel
            {
                DriToDelete = driToDelete,
                DiwsToReassign = await _context.DIWs.Where(d => d.CodeDRI == id).ToListAsync(),
                OtherDris = await _context.DRIs.Where(d => d.CodeDRI != id)
                    .Select(d => new SelectListItem { Value = d.CodeDRI, Text = d.LibelleDRI })
                    .ToListAsync()
            };

            if (!viewModel.DiwsToReassign.Any())
            {
                // No DIWs to reassign, can be deleted directly
                _context.DRIs.Remove(driToDelete);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "DRI supprimée avec succès.";
                return RedirectToAction("listdri");
            }

            if (!viewModel.OtherDris.Any())
            {
                TempData["ErrorMessage"] = "Impossible de supprimer la seule DRI existante. Créez une autre DRI pour réassigner les DIWs.";
                return RedirectToAction("listdri");
            }

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDRI(DeleteDriViewModel model)
        {
            // 1. Re-assign DIWs
            foreach (var reassignment in model.DiwReassignments)
            {
                var diwToUpdate = await _context.DIWs.FindAsync(reassignment.Key);
                if (diwToUpdate != null)
                {
                    diwToUpdate.CodeDRI = reassignment.Value;
                }
            }

            // 2. Delete the old DRI
            var driToDelete = await _context.DRIs.FindAsync(model.DriToDelete.CodeDRI);
            if (driToDelete != null)
            {
                _context.DRIs.Remove(driToDelete);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Les DIWs ont été réassignées et la DRI a été supprimée avec succès.";
            return RedirectToAction("listdri");
        }

        // --- USER MANAGEMENT ---

        // GET: Admin/Edit/user-id
        [HttpGet]
        [Authorize(Policy = Permissions.ManageUsers)]
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var viewModel = new EditUserViewModel
            {
                User = user,
                DRIOptions = await _context.DRIs
                                       .OrderBy(d => d.LibelleDRI)
                                       .Select(d => new SelectListItem { Value = d.CodeDRI, Text = d.LibelleDRI })
                                       .ToListAsync(),
                StatutOptions = new List<SelectListItem>
        {
            new SelectListItem { Value = "0", Text = "Utilisateur DIW" },
            new SelectListItem { Value = "1", Text = "Utilisateur DRI" },
            new SelectListItem { Value = "2", Text = "Utilisateur DC" },
            new SelectListItem { Value = "3", Text = "Administrateur" }
        }
            };

            return View(viewModel);
        }

        // POST: Admin/Edit/user-id
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManageUsers)]
        public async Task<IActionResult> Edit(string id, EditUserViewModel viewModel)
        {
            if (id != viewModel.User.ID_User)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var userToUpdate = await _context.Users.FindAsync(id);
                if (userToUpdate == null)
                {
                    return NotFound();
                }

                // Update properties from the form
                userToUpdate.User_name = viewModel.User.User_name;
                userToUpdate.FirstNmUser = viewModel.User.FirstNmUser;
                userToUpdate.LastNmUser = viewModel.User.LastNmUser;
                userToUpdate.MailUser = viewModel.User.MailUser;
                userToUpdate.TelUser = viewModel.User.TelUser;
                userToUpdate.Statut = viewModel.User.Statut;
                userToUpdate.CodeDIW = viewModel.User.CodeDIW; // This holds the structure code

                try
                {
                    _context.Update(userToUpdate);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Utilisateur mis à jour avec succès.";
                    return RedirectToAction(nameof(listuser));
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Handle concurrency error if necessary
                    ModelState.AddModelError("", "Unable to save changes. The user was modified by another administrator.");
                }
            }

            // If model state is invalid, re-populate dropdowns and return to the view
            viewModel.DRIOptions = await _context.DRIs
                                   .OrderBy(d => d.LibelleDRI)
                                   .Select(d => new SelectListItem { Value = d.CodeDRI, Text = d.LibelleDRI })
                                   .ToListAsync();
            viewModel.StatutOptions = new List<SelectListItem>
    {
        new SelectListItem { Value = "0", Text = "Utilisateur DIW" },
        new SelectListItem { Value = "1", Text = "Utilisateur DRI" },
        new SelectListItem { Value = "2", Text = "Utilisateur DC" },
        new SelectListItem { Value = "3", Text = "Administrateur" }
    };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManageUsers)]
        public async Task<IActionResult> ResetPassword(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest("User ID is required.");
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // 1. Generate a new random password
            var newPassword = PasswordHelper.GenerateRandomPassword();

            // 2. Update both the hashed and plain-text password fields
            user.Password = sha256_hash(newPassword);
            user.motdepasse = newPassword; // Kept for your trial version

            // 3. Save the changes to the database
            _context.Update(user);
            await _context.SaveChangesAsync();

            // 4. Return the new password to the front-end in a JSON object
            return Json(new { success = true, newPassword = newPassword });
        }

        // --- CIBLES MANAGEMENT ---
        // [File: AdminController.cs]

        // --- DRI TARGETS MANAGEMENT (Single Year Logic) ---

        [HttpGet]
        [Authorize(Policy = Permissions.ManageOperationalTargets)]
        public async Task<IActionResult> GestionCiblesDri(string selectedDriCode, string selectedYear)
        {
            var driOptions = await _context.DRIs.OrderBy(d => d.LibelleDRI)
                .Select(d => new SelectListItem { Value = d.CodeDRI, Text = d.LibelleDRI }).ToListAsync();

            var yearOptions = new List<SelectListItem>();
            int current = DateTime.Now.Year;
            yearOptions.Add(new SelectListItem { Value = current.ToString(), Text = current.ToString() });
            yearOptions.Add(new SelectListItem { Value = (current + 1).ToString(), Text = (current + 1).ToString() });
            yearOptions.Add(new SelectListItem { Value = (current + 2).ToString(), Text = (current + 2).ToString() });

            var viewModel = new GestionCiblesDriViewModel
            {
                DriOptions = driOptions,
                YearOptions = yearOptions,
                SelectedDriCode = selectedDriCode,
                SelectedYear = string.IsNullOrEmpty(selectedYear) ? current.ToString() : selectedYear,
                CiblesRows = new List<CibleDriRowViewModel>()
            };

            if (!string.IsNullOrEmpty(selectedDriCode) && !string.IsNullOrEmpty(viewModel.SelectedYear))
            {
                int startY = int.Parse(viewModel.SelectedYear);
                var years = new[] { startY.ToString(), (startY + 1).ToString(), (startY + 2).ToString() };

                var indicators = await _context.Indicateurs_DE_PERFORMANCE_OPERATIONNELS.OrderBy(i => i.IdIndicacteur).ToListAsync();
                var existingTargets = await _context.cibles_de_performance_dri
                    .Where(c => c.CodeDRI == selectedDriCode && years.Contains(c.year))
                    .ToListAsync();

                foreach (var ind in indicators)
                {
                    var t1 = existingTargets.FirstOrDefault(c => c.IdIndicacteur == ind.IdIndicacteur && c.year == years[0]);
                    var t2 = existingTargets.FirstOrDefault(c => c.IdIndicacteur == ind.IdIndicacteur && c.year == years[1]);
                    var t3 = existingTargets.FirstOrDefault(c => c.IdIndicacteur == ind.IdIndicacteur && c.year == years[2]);

                    viewModel.CiblesRows.Add(new CibleDriRowViewModel
                    {
                        IdIndicacteur = ind.IdIndicacteur,
                        IntituleIndicateur = ind.IntituleIn,
                        IdCible1 = t1?.Id_cible ?? 0,
                        ValCible1 = (float)(t1?.cible ?? 0),
                        IdCible2 = t2?.Id_cible ?? 0,
                        ValCible2 = (float)(t2?.cible ?? 0),
                        IdCible3 = t3?.Id_cible ?? 0,
                        ValCible3 = (float)(t3?.cible ?? 0)
                    });
                }
            }
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManageOperationalTargets)]
        public async Task<IActionResult> GestionCiblesDri(GestionCiblesDriViewModel model)
        {
            if (string.IsNullOrEmpty(model.SelectedDriCode) || string.IsNullOrEmpty(model.SelectedYear))
                return RedirectToAction(nameof(GestionCiblesDri));

            int startY = int.Parse(model.SelectedYear);
            string[] years = { startY.ToString(), (startY + 1).ToString(), (startY + 2).ToString() };

            foreach (var row in model.CiblesRows)
            {
                async Task SaveDri(string y, int idCible, float val)
                {
                    if (idCible != 0)
                    {
                        var t = await _context.cibles_de_performance_dri.FindAsync(idCible);
                        if (t != null) t.cible = val;
                    }
                    else
                    {
                        // Verify existence before adding
                        bool exists = await _context.cibles_de_performance_dri.AnyAsync(c => c.CodeDRI == model.SelectedDriCode && c.IdIndicacteur == row.IdIndicacteur && c.year == y);
                        if (!exists)
                        {
                            _context.cibles_de_performance_dri.Add(new cibles_de_performance_dri
                            {
                                CodeDRI = model.SelectedDriCode,
                                IdIndicacteur = row.IdIndicacteur,
                                year = y,
                                cible = val
                            });
                        }
                    }
                }
                await SaveDri(years[0], row.IdCible1, row.ValCible1);
                await SaveDri(years[1], row.IdCible2, row.ValCible2);
                await SaveDri(years[2], row.IdCible3, row.ValCible3);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Cibles DRI pour la période {years[0]}-{years[2]} enregistrées.";
            return RedirectToAction(nameof(GestionCiblesDri), new { selectedDriCode = model.SelectedDriCode, selectedYear = model.SelectedYear });
        }
        // YEAR_MOD: This is the updated GET action for strategic target management.
        [HttpGet]
        [Authorize(Policy = Permissions.ManageStrategicTargets)]
        public async Task<IActionResult> GestionCiblesStrat(string selectedYear)
        {
            string startYearStr = string.IsNullOrEmpty(selectedYear) ? DateTime.Now.Year.ToString() : selectedYear;
            int startYear = int.Parse(startYearStr);
            var years = new[] { startYear.ToString(), (startYear + 1).ToString(), (startYear + 2).ToString() };

            var allIndicators = await _context.IndicateursStrategiques
                                      .Include(i => i.Objectif)
                                      .Include(i => i.CategorieIndicateur)
                                      .OrderBy(i => i.IdIndic)
                                      .ToListAsync();

            var targets = await _context.ciblesStrategiques
                                        .Where(c => years.Contains(c.year))
                                        .ToListAsync();

            var model = new GestionCiblesStratViewModel
            {
                SelectedYear = startYearStr,
                Cibles = allIndicators.Select(i =>
                {
                    var t1 = targets.FirstOrDefault(c => c.IdIndic == i.IdIndic && c.year == years[0]);
                    var t2 = targets.FirstOrDefault(c => c.IdIndic == i.IdIndic && c.year == years[1]);
                    var t3 = targets.FirstOrDefault(c => c.IdIndic == i.IdIndic && c.year == years[2]);

                    return new CibleStratViewModel
                    {
                        IdIndic = i.IdIndic,
                        AxeName = i.CategorieIndicateur?.IntituleCategIn,
                        ObjectifName = i.Objectif?.Intituleobj,
                        IntituleIn = i.IntituleIn,

                        IdCible1 = t1?.id_cible ?? 0,
                        Cible1 = (float)(t1?.cible ?? 0.0),

                        IdCible2 = t2?.id_cible ?? 0,
                        Cible2 = (float)(t2?.cible ?? 0.0),

                        IdCible3 = t3?.id_cible ?? 0,
                        Cible3 = (float)(t3?.cible ?? 0.0)
                    };
                }).ToList()
            };

            return View(model);
        }

        // YEAR_MOD: This is the updated POST action for strategic target management.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManageStrategicTargets)]
        public async Task<IActionResult> GestionCiblesStrat(GestionCiblesStratViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                int startYear = int.Parse(viewModel.SelectedYear);
                string[] years = { startYear.ToString(), (startYear + 1).ToString(), (startYear + 2).ToString() };

                foreach (var row in viewModel.Cibles)
                {
                    // Local function to handle save logic
                    async Task SaveStrat(string year, long idCible, float valCible)
                    {
                        if (idCible != 0)
                        {
                            var existing = await _context.ciblesStrategiques.FindAsync(idCible);
                            if (existing != null) existing.cible = valCible;
                        }
                        else
                        {
                            // If doesn't exist but has value (or even if 0, to initialize)
                            var existingCheck = await _context.ciblesStrategiques
                                .AnyAsync(c => c.IdIndic == row.IdIndic && c.year == year);

                            if (!existingCheck)
                            {
                                _context.ciblesStrategiques.Add(new cible_stratigique
                                {
                                    IdIndic = row.IdIndic,
                                    year = year,
                                    cible = valCible
                                });
                            }
                        }
                    }

                    await SaveStrat(years[0], row.IdCible1, row.Cible1);
                    await SaveStrat(years[1], row.IdCible2, row.Cible2);
                    await SaveStrat(years[2], row.IdCible3, row.Cible3);
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Cibles stratégiques (Période {years[0]}-{years[2]}) mises à jour avec succès.";
                return RedirectToAction(nameof(GestionCiblesStrat), new { selectedYear = viewModel.SelectedYear });
            }
            return View(viewModel);
        }

        // YEAR_MOD: This is the updated GET action for operational target management.
        [HttpGet]
        [Authorize(Policy = Permissions.ManageOperationalTargets)]
        public async Task<IActionResult> GestionCiblesOps(string selectedDiwCode, string selectedYear)
        {
            var diwItems = new List<SelectListItem>();
            var dris = await _context.DRIs
                                     .Include(d => d.DIWs)
                                     .OrderBy(d => d.LibelleDRI)
                                     .ToListAsync();

            foreach (var dri in dris)
            {
                var group = new SelectListGroup { Name = dri.LibelleDRI };
                foreach (var diw in dri.DIWs.OrderBy(w => w.LibelleDIW))
                {
                    diwItems.Add(new SelectListItem
                    {
                        Value = diw.CodeDIW,
                        Text = diw.LibelleDIW,
                        Group = group
                    });
                }
            }

            var viewModel = new GestionCiblesOpsViewModel
            {
                DiwOptions = diwItems,
                SelectedDiwCode = selectedDiwCode,
                SelectedYear = string.IsNullOrEmpty(selectedYear) ? DateTime.Now.Year.ToString() : selectedYear,
                Cibles = new List<CibleOpViewModel>()
            };

            return View(viewModel);
        }

        // YEAR_MOD: This is the updated POST action for operational target management.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManageOperationalTargets)]
        public async Task<IActionResult> GestionCiblesOps(GestionCiblesOpsViewModel viewModel)
        {
            if (viewModel.Cibles != null && viewModel.Cibles.Any())
            {
                foreach (var row in viewModel.Cibles)
                {
                    // Update Year 1
                    if (row.id_cible1 != 0)
                    {
                        var c1 = await _context.cibles.FindAsync(row.id_cible1);
                        if (c1 != null) c1.cible = row.cible1;
                    }
                    // Update Year 2
                    if (row.id_cible2 != 0)
                    {
                        var c2 = await _context.cibles.FindAsync(row.id_cible2);
                        if (c2 != null) c2.cible = row.cible2;
                    }
                    // Update Year 3
                    if (row.id_cible3 != 0)
                    {
                        var c3 = await _context.cibles.FindAsync(row.id_cible3);
                        if (c3 != null) c3.cible = row.cible3;
                    }
                }
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Cibles pour la période {viewModel.SelectedYear}-{int.Parse(viewModel.SelectedYear) + 2} mises à jour avec succès.";
            }

            return RedirectToAction(nameof(GestionCiblesOps), new { selectedDiwCode = viewModel.SelectedDiwCode, selectedYear = viewModel.SelectedYear });
        }


        [HttpGet]
        public IActionResult IndicateursIndex()
        {
            return View();
        }

        // --- INDICATOR MANAGEMENT ---

        // [File: AdminController.cs]

        [HttpGet]
        [Authorize(Policy = Permissions.ManageStrategicIndicators)]
        public async Task<IActionResult> ListIndicateursStrat()
        {
            string currentYear = DateTime.Now.Year.ToString();

            var viewModel = await _context.IndicateursStrategiques
                                        .Include(i => i.CategorieIndicateur)
                                        .Include(i => i.Objectif)
                                        .OrderBy(i => i.IdIndic)
                                        .Select(i => new ListIndicateurStratViewModel
                                        {
                                            IdIndic = i.IdIndic,
                                            AxeName = i.CategorieIndicateur.IntituleCategIn,
                                            ObjectifName = i.Objectif.Intituleobj,
                                            IntituleIn = i.IntituleIn,
                                            // This finds the target for the current year from the related table.
                                            CibleAnneeEnCours = (float?)(double?)i.CiblesStrategiques
                                                                                  .FirstOrDefault(c => c.year == currentYear)
                                                                                  .cible
                                        })
                                        .ToListAsync();

            // Pass the new list of ViewModels to the view.
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> AddIndicateurStrat()
        {
            var viewModel = new AddIndicateurStratViewModel
            {
                Indicateur = new IndicateurStrategique(),
                Categories = await _context.CategorieIndicateurs
                                         .OrderBy(c => c.IntituleCategIn)
                                         .Select(c => new SelectListItem { Value = c.IdCategIn, Text = c.IntituleCategIn })
                                         .ToListAsync(),
                Objectifs = new List<SelectListItem>() // Will be populated by JavaScript
            };
            return View(viewModel);
        }

        // [File: AdminController.cs]

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddIndicateurStrat(AddIndicateurStratViewModel viewModel)
        {
            bool exists = await _context.IndicateursStrategiques.AnyAsync(i =>
                i.IntituleIn == viewModel.Indicateur.IntituleIn &&
                i.IdCategIn == viewModel.Indicateur.IdCategIn &&
                i.idobj == viewModel.Indicateur.idobj);

            if (exists)
            {
                ModelState.AddModelError("Indicateur.IntituleIn", "Un indicateur avec cet intitulé existe déjà pour cet axe et objectif.");
            }

            if (ModelState.IsValid)
            {
                // Auto-generate the new Indicator ID
                var indicatorsInCategory = await _context.IndicateursStrategiques
                    .Where(i => i.IdCategIn == viewModel.Indicateur.IdCategIn)
                    .ToListAsync();

                int maxNumber = 0;
                if (indicatorsInCategory.Any())
                {
                    foreach (var indicator in indicatorsInCategory)
                    {
                        if (indicator.IdIndic.Contains("."))
                        {
                            string numberStr = indicator.IdIndic.Split('.').Last();
                            if (int.TryParse(numberStr, out int currentNumber))
                            {
                                if (currentNumber > maxNumber)
                                {
                                    maxNumber = currentNumber;
                                }
                            }
                        }
                    }
                }
                int nextNumber = maxNumber + 1;
                viewModel.Indicateur.IdIndic = $"{viewModel.Indicateur.IdCategIn}.{nextNumber}";

                // Step 1: Add and save the new indicator first.
                _context.IndicateursStrategiques.Add(viewModel.Indicateur);
                await _context.SaveChangesAsync(); // This saves the indicator and makes its ID available.

                // Step 2: ✨ Create and save the initial target for the current year.
                var initialTarget = new cible_stratigique
                {
                    IdIndic = viewModel.Indicateur.IdIndic, // Use the ID from the newly created indicator
                    cible = viewModel.InitialCible,
                    year = DateTime.Now.Year.ToString()
                };
                _context.ciblesStrategiques.Add(initialTarget);
                await _context.SaveChangesAsync(); // This saves the new target.

                TempData["SuccessMessage"] = "Indicateur stratégique et sa cible initiale ont été ajoutés avec succès.";
                return RedirectToAction(nameof(ListIndicateursStrat));
            }

            // If model state is invalid, repopulate dropdowns and return to view
            viewModel.Categories = await _context.CategorieIndicateurs
                                             .OrderBy(c => c.IntituleCategIn)
                                             .Select(c => new SelectListItem { Value = c.IdCategIn, Text = c.IntituleCategIn })
                                             .ToListAsync();
            viewModel.Objectifs = new List<SelectListItem>();
            return View(viewModel);
        }
        // Helper API to get objectives for the dynamic dropdown
        [HttpGet]
        public async Task<JsonResult> GetObjectivesByCategory(string categoryId)
        {
            var objectifs = await _context.Objectifs
                                        .Where(o => o.IdCategIn == categoryId)
                                        .OrderBy(o => o.Intituleobj)
                                        .Select(o => new { value = o.idobj, text = o.Intituleobj })
                                        .ToListAsync();
            return Json(objectifs);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteIndicateurStrat(string id)
        {
            var indicateur = await _context.IndicateursStrategiques.FindAsync(id);
            if (indicateur == null)
            {
                return NotFound();
            }

            // ÉTAPE 1 : Supprimer d'abord les cibles liées (enfants)
            var ciblesLiees = await _context.ciblesStrategiques
                                            .Where(c => c.IdIndic == id)
                                            .ToListAsync();

            if (ciblesLiees.Any())
            {
                _context.ciblesStrategiques.RemoveRange(ciblesLiees);
            }

            // ÉTAPE 2 : Supprimer l'indicateur (parent) maintenant qu'il n'a plus de liens
            _context.IndicateursStrategiques.Remove(indicateur);

            // ÉTAPE 3 : Sauvegarder le tout en une seule transaction
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Indicateur stratégique et ses historiques de cibles supprimés avec succès.";
            return RedirectToAction(nameof(ListIndicateursStrat));
        }

        // --- OPERATIONAL INDICATOR ACTIONS ---

        [HttpGet]
        [Authorize(Policy = Permissions.ManageOperationalIndicators)]
        public async Task<IActionResult> ListIndicateursOps()
        {
            var indicateurs = await _context.Indicateurs
                                        .Include(i => i.CategorieIndicateur)
                                        .OrderBy(i => i.IdIn)
                                        .ToListAsync();
            return View(indicateurs);
        }

        [HttpGet]
        public async Task<IActionResult> AddIndicateurOps()
        {
            var viewModel = new AddIndicateurOpsViewModel
            {
                Indicateur = new Indicateur(),
                Categories = await _context.CategorieIndicateurs
                                         .OrderBy(c => c.IntituleCategIn)
                                         .Select(c => new SelectListItem { Value = c.IdCategIn, Text = c.IntituleCategIn })
                                         .ToListAsync()
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddIndicateurOps(AddIndicateurOpsViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                // --- CORRECTED: Auto-generate the new Indicator ID ---
                var indicatorsInCategory = await _context.Indicateurs
                    .Where(i => i.IdCategIn == viewModel.Indicateur.IdCategIn)
                    .ToListAsync();

                int maxNumber = 0;
                if (indicatorsInCategory.Any())
                {
                    // Loop in memory to find the true maximum number after the dot
                    foreach (var indicator in indicatorsInCategory)
                    {
                        if (indicator.IdIn.Contains("."))
                        {
                            string numberStr = indicator.IdIn.Split('.').Last();
                            if (int.TryParse(numberStr, out int currentNumber))
                            {
                                if (currentNumber > maxNumber)
                                {
                                    maxNumber = currentNumber;
                                }
                            }
                        }
                    }
                }
                int nextNumber = maxNumber + 1;
                viewModel.Indicateur.IdIn = $"{viewModel.Indicateur.IdCategIn}.{nextNumber}";
                // --- END OF CORRECTION ---

                // The rest of the logic remains the same
                _context.Indicateurs.Add(viewModel.Indicateur);
                await _context.SaveChangesAsync();

                // YEAR_MOD: When adding a new indicator, we no longer create a single target.
                // Targets will be created on-demand from the GestionCiblesOps page.
                // This prevents creating targets for years that might not be needed.

                TempData["SuccessMessage"] = "Indicateur opérationnel créé avec succès. Veuillez définir ses cibles annuelles dans la section 'Gestion des Cibles'.";
                return RedirectToAction(nameof(ListIndicateursOps));
            }

            viewModel.Categories = await _context.CategorieIndicateurs
                                         .OrderBy(c => c.IntituleCategIn)
                                         .Select(c => new SelectListItem { Value = c.IdCategIn, Text = c.IntituleCategIn })
                                         .ToListAsync();
            return View(viewModel);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteIndicateurOps(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            // --- 1. Find and delete all related 'Cible' records FIRST ---
            var relatedCibles = await _context.cibles.Where(c => c.IdIn == id).ToListAsync();
            if (relatedCibles.Any())
            {
                _context.cibles.RemoveRange(relatedCibles);
                await _context.SaveChangesAsync(); // <-- FIRST SAVE: Commits the deletion of child records
            }

            // --- 2. Now that the child records are gone, delete the Indicator ---
            var indicateur = await _context.Indicateurs.FindAsync(id);
            if (indicateur != null)
            {
                _context.Indicateurs.Remove(indicateur);
                await _context.SaveChangesAsync(); // <-- SECOND SAVE: Commits the deletion of the parent record
            }

            TempData["SuccessMessage"] = "Indicateur opérationnel et toutes ses cibles associées ont été supprimés.";
            return RedirectToAction(nameof(ListIndicateursOps));
        }
        [Authorize(Policy = Permissions.ViewValidatedReports)]
        public async Task<IActionResult> ExportToExcel(string id)
        {
            var situation = await _context.Situations.FindAsync(id);
            if (situation == null) return NotFound();

            var user = await _context.Users.FindAsync(situation.User_id);
            if (user == null) return NotFound();

            // --- CORRECTED LOGIC ---
            // Check which type of report it is and call the correct service

            // Case 1: Strategic (DC) Report
            if (user.Statut == (int)UserRole.DC)
            {
                var (fileContents, fileName) = await _reportService.GenerateStrategicExcelAsync(id);
                return File(fileContents, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }

            // Case 2: Check if it's a DRI Self-Report
            bool isDriSelfReport = await _context.DeclarationDRIs.AnyAsync(d => d.IDSituation == id);
            if (isDriSelfReport)
            {
                var (fileContents, fileName) = await _reportService.GenerateDriExcelAsync(id);
                return File(fileContents, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }

            // Case 3: Otherwise, it's a standard Operational (DIW) Report
            else
            {
                var (fileContents, fileName) = await _reportService.GenerateOperationalExcelAsync(id);
                return File(fileContents, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }

        [Authorize(Policy = Permissions.ViewValidatedReports)]
        public async Task<IActionResult> ExportToPdf(string id)
        {
            var situation = await _context.Situations.FindAsync(id);
            if (situation == null) return NotFound();

            var user = await _context.Users.FindAsync(situation.User_id);
            if (user == null) return NotFound();

            // --- CORRECTED LOGIC ---
            // Check which type of report it is and call the correct service

            // Case 1: Strategic (DC) Report
            if (user.Statut == (int)UserRole.DC)
            {
                var (fileContents, fileName) = await _reportService.GenerateStrategicPdfAsync(id);
                return File(fileContents, "application/pdf", fileName);
            }

            // Case 2: Check if it's a DRI Self-Report
            bool isDriSelfReport = await _context.DeclarationDRIs.AnyAsync(d => d.IDSituation == id);
            if (isDriSelfReport)
            {
                var (fileContents, fileName) = await _reportService.GenerateDriPdfAsync(id);
                return File(fileContents, "application/pdf", fileName);
            }

            // Case 3: Otherwise, it's a standard Operational (DIW) Report
            else
            {
                var (fileContents, fileName) = await _reportService.GenerateOperationalPdfAsync(id);
                return File(fileContents, "application/pdf", fileName);
            }
        }

        [HttpGet]
        [Authorize(Policy = Permissions.ViewOperationalAnalysis)]
        public async Task<IActionResult> NiveauOperationnel(NiveauOpViewModel filters)
        {
            var viewModel = new NiveauOpViewModel
            {
                // 1. Populate all filter dropdowns
                YearOptions = await _context.Situations.Select(s => s.Year).Distinct().OrderByDescending(y => y).Select(y => new SelectListItem { Value = y, Text = y }).ToListAsync(),
                DriOptions = await _context.DRIs.OrderBy(d => d.LibelleDRI).Select(d => new SelectListItem { Value = d.CodeDRI, Text = d.LibelleDRI }).ToListAsync(),
                AxeOptions = await _context.CategorieIndicateurs.OrderBy(c => c.IntituleCategIn).Select(c => new SelectListItem { Value = c.IdCategIn, Text = c.IntituleCategIn }).ToListAsync(),
                IndicateurOptions = await _context.Indicateurs
                    .Where(i => string.IsNullOrEmpty(filters.SelectedAxe) || i.IdCategIn == filters.SelectedAxe)
                    .OrderBy(i => i.IntituleIn)
                    .Select(i => new SelectListItem { Value = i.IdIn, Text = i.IntituleIn })
                    .ToListAsync(),
                DiwOptions = new List<SelectListItem>(),
                PerformanceResults = new List<IndicatorPerformanceViewModel>(),

                // Restore selected filter values
                SelectedYear = filters.SelectedYear,
                SelectedSemester = filters.SelectedSemester,
                SelectedTrimester = filters.SelectedTrimester,
                SelectedMonth = filters.SelectedMonth,
                SelectedDri = filters.SelectedDri,
                SelectedDiw = filters.SelectedDiw,
                SelectedAxe = filters.SelectedAxe,
                SelectedIndicateur = filters.SelectedIndicateur
            };

            // If a DRI was selected, repopulate the DIW dropdown for it
            if (!string.IsNullOrEmpty(filters.SelectedDri))
            {
                viewModel.DiwOptions = await _context.DIWs
                    .Where(d => d.CodeDRI == filters.SelectedDri)
                    .OrderBy(d => d.LibelleDIW)
                    .Select(d => new SelectListItem { Value = d.CodeDIW, Text = d.LibelleDIW })
                    .ToListAsync();
            }

            // --- CORRECTED LOGIC ---
            // Check specifically if a DRI has been selected. This is now the main trigger.
            if (string.IsNullOrEmpty(filters.SelectedDri))
            {
                viewModel.IsSearchPerformed = false; // Do not perform search
                return View(viewModel);
            }

            viewModel.IsSearchPerformed = true;

            // 2. Build and apply the rest of the query (this logic is unchanged)
            IQueryable<Declaration> query = _context.Declarations
                .Include(d => d.Situation)
                .Include(d => d.Indicateur)
                .ThenInclude(i => i.CategorieIndicateur);

            if (!string.IsNullOrEmpty(filters.SelectedYear)) query = query.Where(d => d.Situation.Year == filters.SelectedYear);
            if (!string.IsNullOrEmpty(filters.SelectedAxe)) query = query.Where(d => d.Indicateur.IdCategIn == filters.SelectedAxe);
            if (!string.IsNullOrEmpty(filters.SelectedIndicateur)) query = query.Where(d => d.IdIn == filters.SelectedIndicateur);
            if (!string.IsNullOrEmpty(filters.SelectedDiw))
            {
                query = query.Where(d => d.Situation.DIW == filters.SelectedDiw);
            }
            else if (!string.IsNullOrEmpty(filters.SelectedDri))
            {
                var diwCodesInDri = viewModel.DiwOptions.Select(d => d.Value).ToList();
                query = query.Where(d => diwCodesInDri.Contains(d.Situation.DIW));
            }

            // ... date filters ...
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

            // 3. Fetch and process results (this logic is unchanged)
            var filteredDeclarations = await query.ToListAsync();
            viewModel.PerformanceResults = filteredDeclarations
                .GroupBy(d => d.Indicateur)
                .Select(g =>
                {
                    double totalNumerateur = g.Sum(d => d.Numerateur ?? 0);
                    double totalDenominateur = g.Sum(d => d.Denominateur ?? 0);
                    double taux = (totalDenominateur > 0) ? (totalNumerateur / totalDenominateur) * 100 : 0;
                    // YEAR_MOD: Cible is now taken from the declaration itself, which is correct.
                    double cible = g.Average(d => d.Cible ?? 0);
                    return new IndicatorPerformanceViewModel
                    {
                        AxeName = g.Key.CategorieIndicateur.IntituleCategIn,
                        IndicatorName = g.Key.IntituleIn,
                        SumNumerateur = totalNumerateur,         
                        SumDenominateur = totalDenominateur,
                        Taux = taux,
                        Cible = cible,
                        Ecart = taux - cible
                    };
                })
                .OrderBy(r => r.AxeName).ThenBy(r => r.IndicatorName)
                .ToList();

            return View(viewModel);
        }
        // Helper API to get indicators for the dynamic dropdown
        [HttpGet]
        public async Task<JsonResult> GetIndicatorsByAxe(string axeId)
        {
            // Start with the base query
            var query = _context.Indicateurs.AsQueryable();

            // If a specific Axe is selected, filter by it
            if (!string.IsNullOrEmpty(axeId))
            {
                query = query.Where(i => i.IdCategIn == axeId);
            }

            // Select the data needed for the dropdown and return as JSON
            var indicators = await query
                .OrderBy(i => i.IntituleIn)
                .Select(i => new { value = i.IdIn, text = i.IntituleIn })
                .ToListAsync();

            return Json(indicators);
        }

        // [File: AdminController.cs]

        [HttpGet]
        [Authorize(Policy = Permissions.ViewStrategicAnalysis)]
        public async Task<IActionResult> NiveauStrategique(NiveauStratViewModel filters)
        {
            // ... (The beginning of the method remains the same) ...
            if (string.IsNullOrEmpty(filters.SelectedAxe))
            {
                filters.SelectedObjectif = null;
            }

            var viewModel = new NiveauStratViewModel
            {
                YearOptions = await _context.Situations.Select(s => s.Year).Distinct().OrderByDescending(y => y).Select(y => new SelectListItem { Value = y.ToString(), Text = y.ToString() }).ToListAsync(),
                AxeOptions = await _context.CategorieIndicateurs.OrderBy(c => c.IntituleCategIn).Select(c => new SelectListItem { Value = c.IdCategIn, Text = c.IntituleCategIn }).ToListAsync(),
                ObjectifOptions = new List<SelectListItem>(),
                PerformanceResults = new List<IndicatorStratPerformanceViewModel>(),
                SelectedYear = filters.SelectedYear,
                SelectedSemester = filters.SelectedSemester,
                SelectedTrimester = filters.SelectedTrimester,
                SelectedMonth = filters.SelectedMonth,
                SelectedAxe = filters.SelectedAxe,
                SelectedObjectif = filters.SelectedObjectif
            };

            if (!string.IsNullOrEmpty(filters.SelectedAxe))
            {
                viewModel.ObjectifOptions = await _context.Objectifs
                    .Where(o => o.IdCategIn == filters.SelectedAxe)
                    .OrderBy(o => o.Intituleobj)
                    .Select(o => new SelectListItem { Value = o.idobj.ToString(), Text = o.Intituleobj })
                    .ToListAsync();
            }

            bool isAnyFilterApplied = !string.IsNullOrEmpty(filters.SelectedYear) ||
                                      filters.SelectedSemester.HasValue ||
                                      filters.SelectedTrimester.HasValue ||
                                      filters.SelectedMonth.HasValue ||
                                      !string.IsNullOrEmpty(filters.SelectedAxe) ||
                                      filters.SelectedObjectif.HasValue;

            viewModel.IsSearchPerformed = isAnyFilterApplied;

            IQueryable<DeclarationStrategique> query = _context.DeclarationsStrategiques
                .Include(d => d.Situation)
                .Include(d => d.IndicateurStrategique)
                    .ThenInclude(i => i.CategorieIndicateur)
                .Include(d => d.IndicateurStrategique)
                    .ThenInclude(i => i.Objectif);

            if (isAnyFilterApplied)
            {
                if (!string.IsNullOrEmpty(filters.SelectedYear))
                    query = query.Where(d => d.Situation.Year == filters.SelectedYear);

                if (!string.IsNullOrEmpty(filters.SelectedAxe))
                    query = query.Where(d => d.IndicateurStrategique.IdCategIn == filters.SelectedAxe);

                if (filters.SelectedObjectif.HasValue)
                    query = query.Where(d => d.IndicateurStrategique.idobj == filters.SelectedObjectif.Value);

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
            }
            else
            {
                var dcUserIds = await _context.Users.Where(u => u.Statut == 2).Select(u => u.ID_User).ToListAsync();
                var lastValidatedDcSituationIds = await _context.Situations
                    .Where(s => s.Statut == 3 && dcUserIds.Contains(s.User_id))
                    .GroupBy(s => s.User_id)
                    .Select(g => g.OrderByDescending(s => s.ConfirmDate).First().IDSituation)
                    .ToListAsync();

                query = query.Where(d => lastValidatedDcSituationIds.Contains(d.IDSituation));
            }

            // ✨ THIS IS THE CORRECTED QUERY
            // It no longer references the old Cible property and instead aggregates
            // the Cible value from the declarations themselves.
            var aggregatedData = await query
                .GroupBy(d => new {
                    IndicatorName = d.IndicateurStrategique.IntituleIn,
                    AxeName = d.IndicateurStrategique.CategorieIndicateur.IntituleCategIn,
                    ObjectifName = d.IndicateurStrategique.Objectif.Intituleobj
                })
                .Select(g => new {
                    g.Key.AxeName,
                    g.Key.ObjectifName,
                    g.Key.IndicatorName,
                    AverageCible = g.Average(d => d.Cible), // Use the average of the targets from the declarations
                    TotalNumerateur = g.Sum(d => d.Numerateur),
                    TotalDenominateur = g.Sum(d => d.Denominateur)
                })
                .OrderBy(r => r.AxeName).ThenBy(r => r.ObjectifName).ThenBy(r => r.IndicatorName)
                .ToListAsync();

            viewModel.PerformanceResults = aggregatedData.Select(r => {
                double taux = (r.TotalDenominateur > 0) ? ((double)r.TotalNumerateur / (double)r.TotalDenominateur) * 100 : 0;
                double cible = r.AverageCible ?? 0;
                return new IndicatorStratPerformanceViewModel
                {
                    AxeName = r.AxeName,
                    ObjectifName = r.ObjectifName,
                    IndicatorName = r.IndicatorName,
                    SumNumerateur = (double)(r.TotalNumerateur ?? 0),     
                    SumDenominateur = (double)(r.TotalDenominateur ?? 0),
                    Taux = taux,
                    Cible = cible,
                    Ecart = taux - cible
                };
            }).ToList();

            return View(viewModel);
        }
        [HttpGet]

        public async Task<IActionResult> GetObjectifsByAxeJson(string axeId)
        {
            // Check for invalid input
            if (string.IsNullOrEmpty(axeId))
            {
                return BadRequest("Axe ID is required.");
            }

            try
            {
                var objectifs = await _context.Objectifs
                    .Where(o => o.IdCategIn == axeId)
                    .OrderBy(o => o.Intituleobj)
                    .Select(o => new {
                        value = o.idobj.ToString(),
                        text = o.Intituleobj
                    })
                    .ToListAsync();

                // This will return an empty list [] if none are found, which is valid JSON.
                return Json(objectifs);
            }
            catch (Exception ex)
            {
                // Log the actual error for debugging
                // _logger.LogError(ex, "An error occurred while fetching objectifs for Axe ID {AxeId}", axeId);

                // Return a server error status code. The JavaScript .catch() block will handle this.
                return StatusCode(500, "An internal server error occurred.");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            var viewModel = new ProfileViewModel
            {
                UserProfile = user,
                RoleName = "Administrateur",
                StructureName = "Accès Global", // Admin has global access
                ParentStructureName = "Système"
            };

            // We will reuse the same professional view we already built
            return View("~/Views/Shared/Profile.cshtml", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> UpdateProfileInfo(string username, string email, string phone)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(phone))
            {
                return Json(new { success = false, message = "Tous les champs sont requis." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userToUpdate = await _context.Users.FindAsync(userId);
            if (userToUpdate == null) return Json(new { success = false, message = "Utilisateur non trouvé." });

            if (userToUpdate.User_name != username && await _context.Users.AnyAsync(u => u.User_name == username))
            {
                return Json(new { success = false, message = "Ce nom d'utilisateur est déjà pris." });
            }

            userToUpdate.User_name = username;
            userToUpdate.MailUser = email;
            userToUpdate.TelUser = phone;

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Informations mises à jour avec succès." });
        }

        // [File: AdminController.cs]

        [HttpGet]
        [Authorize(Policy = Permissions.ManageOperationalTargets)]
        public async Task<IActionResult> GetCiblesForDiwJson(string diwCode, string year)
        {
            if (string.IsNullOrEmpty(diwCode) || string.IsNullOrEmpty(year))
            {
                return Json(new List<CibleOpViewModel>());
            }

            try
            {
                int startYear = int.Parse(year);
                string y1 = startYear.ToString();
                string y2 = (startYear + 1).ToString();
                string y3 = (startYear + 2).ToString();
                var years = new[] { y1, y2, y3 };

                // 1. Get all indicators
                var allIndicators = await _context.Indicateurs
                    .Include(i => i.CategorieIndicateur)
                    .OrderBy(i => i.CategorieIndicateur.IntituleCategIn)
                    .ThenBy(i => i.IdIn)
                    .ToListAsync();

                // 2. Get existing targets for all 3 years
                var existingCibles = await _context.cibles
                    .Where(c => c.CodeDIW == diwCode && years.Contains(c.year))
                    .ToListAsync();

                var viewModelResult = new List<CibleOpViewModel>();
                var ciblesToCreate = new List<Cible>();

                // 3. Loop through indicators and map/create targets for 3 years
                foreach (var indicator in allIndicators)
                {
                    var row = new CibleOpViewModel
                    {
                        CodeDIW = diwCode,
                        IdIn = indicator.IdIn,
                        IntituleIn = indicator.IntituleIn,
                        AxeName = indicator.CategorieIndicateur?.IntituleCategIn ?? "Axe non défini"
                    };

                    // Helper function to find or create a target
                    Cible GetOrCreate(string y)
                    {
                        var target = existingCibles.FirstOrDefault(c => c.IdIn == indicator.IdIn && c.year == y);
                        if (target == null)
                        {
                            target = new Cible { CodeDIW = diwCode, IdIn = indicator.IdIn, year = y, cible = 0 };
                            ciblesToCreate.Add(target); // Queue for insert
                        }
                        return target;
                    }

                    var t1 = GetOrCreate(y1);
                    var t2 = GetOrCreate(y2);
                    var t3 = GetOrCreate(y3);

                    // Temporarily assign objects to list for saving, will map IDs after save
                    // We can't map IDs yet if they are new (0)
                }

                // 4. Save new records to generate IDs
                if (ciblesToCreate.Any())
                {
                    await _context.cibles.AddRangeAsync(ciblesToCreate);
                    await _context.SaveChangesAsync();
                    // Refresh list from DB to get new IDs
                    existingCibles = await _context.cibles
                       .Where(c => c.CodeDIW == diwCode && years.Contains(c.year))
                       .ToListAsync();
                }

                // 5. Final Mapping
                foreach (var indicator in allIndicators)
                {
                    var t1 = existingCibles.FirstOrDefault(c => c.IdIn == indicator.IdIn && c.year == y1);
                    var t2 = existingCibles.FirstOrDefault(c => c.IdIn == indicator.IdIn && c.year == y2);
                    var t3 = existingCibles.FirstOrDefault(c => c.IdIn == indicator.IdIn && c.year == y3);

                    viewModelResult.Add(new CibleOpViewModel
                    {
                        CodeDIW = diwCode,
                        IdIn = indicator.IdIn,
                        IntituleIn = indicator.IntituleIn,
                        AxeName = indicator.CategorieIndicateur?.IntituleCategIn ?? "Axe non défini",
                        id_cible1 = t1?.id_cible ?? 0,
                        cible1 = t1?.cible ?? 0,
                        id_cible2 = t2?.id_cible ?? 0,
                        cible2 = t2?.cible ?? 0,
                        id_cible3 = t3?.id_cible ?? 0,
                        cible3 = t3?.cible ?? 0
                    });
                }

                return Json(viewModelResult);
            }
            catch (Exception)
            {
                return StatusCode(500, "An internal server error occurred.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManageUsers)]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            // Safety Check 1: Prevent an admin from deleting their own account
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (id == currentUserId)
            {
                TempData["ErrorMessage"] = "Action non autorisée. Vous ne pouvez pas supprimer votre propre compte.";
                return RedirectToAction("listuser");
            }

            // Safety Check 2: Check if the user has created any situations
            bool userHasSituations = await _context.Situations.AnyAsync(s => s.User_id == id);
            if (userHasSituations)
            {
                TempData["ErrorMessage"] = "Impossible de supprimer cet utilisateur car il est associé à des situations existantes. Veuillez d'abord désactiver le compte.";
                return RedirectToAction("listuser");
            }

            var userToDelete = await _context.Users.FindAsync(id);
            if (userToDelete == null)
            {
                return NotFound();
            }

            _context.Users.Remove(userToDelete);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"L'utilisateur '{userToDelete.FirstNmUser} {userToDelete.LastNmUser}' a été supprimé avec succès.";
            return RedirectToAction("listuser");
        }

        // [File: AdminController.cs]

        // --- AXE (CATEGORIE) MANAGEMENT ---

        [HttpGet]
        public async Task<IActionResult> GestionAxes()
        {
            var axes = await _context.CategorieIndicateurs
                .OrderBy(c => c.IdCategIn)
                .ToListAsync();
            return View(axes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAxe(string idCategIn, string intituleCategIn)
        {
            if (!string.IsNullOrEmpty(idCategIn) && !string.IsNullOrEmpty(intituleCategIn))
            {
                if (await _context.CategorieIndicateurs.AnyAsync(c => c.IdCategIn == idCategIn))
                {
                    TempData["ErrorMessage"] = "L'ID de cet axe existe déjà.";
                }
                else
                {
                    var newAxe = new CategorieIndicateur { IdCategIn = idCategIn, IntituleCategIn = intituleCategIn };
                    _context.CategorieIndicateurs.Add(newAxe);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Axe ajouté avec succès.";
                }
            }
            return RedirectToAction(nameof(GestionAxes));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAxe(string id, string intitule)
        {
            var axe = await _context.CategorieIndicateurs.FindAsync(id);
            if (axe != null && !string.IsNullOrEmpty(intitule))
            {
                axe.IntituleCategIn = intitule;
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Axe renommé avec succès." });
            }
            return BadRequest(new { success = false, message = "Erreur lors du renommage." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAxe(string id)
        {
            var axe = await _context.CategorieIndicateurs.FindAsync(id);
            if (axe != null)
            {
                // Check if any indicators or objectives are linked to this axe
                bool isInUse = await _context.Indicateurs.AnyAsync(i => i.IdCategIn == id) ||
                               await _context.Objectifs.AnyAsync(o => o.IdCategIn == id);

                if (isInUse)
                {
                    TempData["ErrorMessage"] = "Impossible de supprimer cet axe car il est utilisé par des indicateurs ou des objectifs.";
                }
                else
                {
                    _context.CategorieIndicateurs.Remove(axe);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Axe supprimé avec succès.";
                }
            }
            return RedirectToAction(nameof(GestionAxes));
        }
        // [Fichier: AdminController.cs]

        // --- OBJECTIF MANAGEMENT ---

        [HttpGet]
        public async Task<IActionResult> GestionObjectifs()
        {
            var viewModel = new GestionObjectifsViewModel
            {
                // Récupère les objectifs existants avec le nom de leur Axe parent
                Objectifs = await _context.Objectifs
                    .Include(o => o.CategorieIndicateur) // Inclut l'Axe
                    .OrderBy(o => o.CategorieIndicateur.IdCategIn)
                    .ThenBy(o => o.Intituleobj)
                    .Select(o => new ObjectifListItemViewModel
                    {
                        IdObjectif = o.idobj,
                        IntituleObjectif = o.Intituleobj,
                        AxeName = o.CategorieIndicateur.IntituleCategIn
                    })
                    .ToListAsync(),

                // Récupère les axes pour le menu déroulant du formulaire d'ajout
                AxesOptions = await _context.CategorieIndicateurs
                    .OrderBy(c => c.IdCategIn)
                    .Select(c => new SelectListItem
                    {
                        Value = c.IdCategIn,
                        Text = c.IntituleCategIn
                    })
                    .ToListAsync()
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddObjectif(string intituleobj, string idCategIn)
        {
            if (string.IsNullOrEmpty(intituleobj) || string.IsNullOrEmpty(idCategIn))
            {
                TempData["ErrorMessage"] = "L'intitulé et l'axe parent sont requis.";
                return RedirectToAction(nameof(GestionObjectifs));
            }

            // Vérifie si un objectif avec le même nom existe déjà pour cet axe
            bool exists = await _context.Objectifs.AnyAsync(o => o.Intituleobj == intituleobj && o.IdCategIn == idCategIn);
            if (exists)
            {
                TempData["ErrorMessage"] = "Cet objectif existe déjà pour l'axe sélectionné.";
            }
            else
            {
                var newObjectif = new Objectif { Intituleobj = intituleobj, IdCategIn = idCategIn };
                _context.Objectifs.Add(newObjectif);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Objectif ajouté avec succès.";
            }

            return RedirectToAction(nameof(GestionObjectifs));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditObjectif(int id, string intitule)
        {
            var objectif = await _context.Objectifs.FindAsync(id);
            if (objectif != null && !string.IsNullOrEmpty(intitule))
            {
                objectif.Intituleobj = intitule;
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Objectif renommé avec succès." });
            }
            return BadRequest(new { success = false, message = "Erreur lors du renommage de l'objectif." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteObjectif(int id)
        {
            var objectif = await _context.Objectifs.FindAsync(id);
            if (objectif != null)
            {
                // Vérifie si l'objectif est utilisé par des indicateurs stratégiques
                bool isInUse = await _context.IndicateursStrategiques.AnyAsync(i => i.idobj == id);
                if (isInUse)
                {
                    TempData["ErrorMessage"] = "Impossible de supprimer cet objectif car il est lié à des indicateurs stratégiques.";
                }
                else
                {
                    _context.Objectifs.Remove(objectif);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Objectif supprimé avec succès.";
                }
            }
            return RedirectToAction(nameof(GestionObjectifs));
        }

        // [Fichier: AdminController.cs]

        // --- OPERATIONAL INDICATOR ACTIONS ---

        [HttpGet]
        public async Task<IActionResult> EditIndicateurOps(string id)
        {
            if (id == null) return NotFound();

            var indicateur = await _context.Indicateurs.FindAsync(id);
            if (indicateur == null) return NotFound();

            var viewModel = new EditIndicateurOpsViewModel
            {
                Indicateur = indicateur,
                AxeOptions = await _context.CategorieIndicateurs
                    .OrderBy(c => c.IdCategIn)
                    .Select(c => new SelectListItem
                    {
                        Value = c.IdCategIn,
                        Text = c.IntituleCategIn
                    })
                    .ToListAsync()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditIndicateurOps(EditIndicateurOpsViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                // If the model is not valid, reload the necessary data and return to the view
                viewModel.AxeOptions = await _context.CategorieIndicateurs
                    .OrderBy(c => c.IdCategIn)
                    .Select(c => new SelectListItem { Value = c.IdCategIn, Text = c.IntituleCategIn })
                    .ToListAsync();
                return View(viewModel);
            }

            var indicateurToUpdate = await _context.Indicateurs.FindAsync(viewModel.Indicateur.IdIn);
            if (indicateurToUpdate == null) return NotFound();

            // Update the properties
            indicateurToUpdate.IntituleIn = viewModel.Indicateur.IntituleIn;
            indicateurToUpdate.IdCategIn = viewModel.Indicateur.IdCategIn;

            // Note: Changing the Axe will make the ID (e.g., 'AXE1.1') inconsistent
            // if the new Axe is 'AXE2'. This is acceptable based on your request.

            _context.Update(indicateurToUpdate);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Indicateur opérationnel mis à jour avec succès.";
            return RedirectToAction(nameof(ListIndicateursOps));
        }

        // [Fichier: AdminController.cs]

        // --- STRATEGIC INDICATOR ACTIONS ---

        [HttpGet]
        public async Task<IActionResult> EditIndicateurStrat(string id)
        {
            if (id == null) return NotFound();

            var indicateur = await _context.IndicateursStrategiques.FindAsync(id);
            if (indicateur == null) return NotFound();

            var viewModel = new EditIndicateurStratViewModel
            {
                Indicateur = indicateur,
                AxeOptions = await _context.CategorieIndicateurs
                    .OrderBy(c => c.IdCategIn)
                    .Select(c => new SelectListItem { Value = c.IdCategIn, Text = c.IntituleCategIn })
                    .ToListAsync(),
                // Charge uniquement les objectifs de l'axe actuel de l'indicateur
                ObjectifOptions = await _context.Objectifs
                    .Where(o => o.IdCategIn == indicateur.IdCategIn)
                    .OrderBy(o => o.Intituleobj)
                    .Select(o => new SelectListItem { Value = o.idobj.ToString(), Text = o.Intituleobj })
                    .ToListAsync()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditIndicateurStrat(EditIndicateurStratViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                // Si le modèle n'est pas valide, rechargez les menus déroulants
                viewModel.AxeOptions = await _context.CategorieIndicateurs
                    .OrderBy(c => c.IdCategIn)
                    .Select(c => new SelectListItem { Value = c.IdCategIn, Text = c.IntituleCategIn })
                    .ToListAsync();
                viewModel.ObjectifOptions = await _context.Objectifs
                    .Where(o => o.IdCategIn == viewModel.Indicateur.IdCategIn)
                    .OrderBy(o => o.Intituleobj)
                    .Select(o => new SelectListItem { Value = o.idobj.ToString(), Text = o.Intituleobj })
                    .ToListAsync();
                return View(viewModel);
            }

            var indicateurToUpdate = await _context.IndicateursStrategiques.FindAsync(viewModel.Indicateur.IdIndic);
            if (indicateurToUpdate == null) return NotFound();

            // Mettre à jour les propriétés. L'ID ne change pas.
            indicateurToUpdate.IntituleIn = viewModel.Indicateur.IntituleIn;
            indicateurToUpdate.IdCategIn = viewModel.Indicateur.IdCategIn;
            indicateurToUpdate.idobj = viewModel.Indicateur.idobj;

            _context.Update(indicateurToUpdate);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Indicateur stratégique mis à jour avec succès.";
            return RedirectToAction(nameof(ListIndicateursStrat));
        }

        // [File: AdminController.cs]

        // --- DC (Direction Centrale) MANAGEMENT ---

        [HttpGet]
        [Authorize(Policy = Permissions.ManageDCs)] // We'll assume a new permission
        public async Task<IActionResult> GestionDCs()
        {
            var dcs = await _context.DCs.OrderBy(d => d.CodeDC).ToListAsync();
            return View(dcs);
        }

        // [File: AdminController.cs]

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManageDCs)]
        // ✨ Add the description parameter
        public async Task<IActionResult> AddDC(string codeDC, string libelleDC, string description)
        {
            if (!string.IsNullOrEmpty(codeDC) && !string.IsNullOrEmpty(libelleDC))
            {
                if (await _context.DCs.AnyAsync(d => d.CodeDC == codeDC))
                {
                    TempData["ErrorMessage"] = "Le code de cette DC existe déjà.";
                }
                else
                {
                    // ✨ Add the description to the new object
                    var newDC = new DC { CodeDC = codeDC, LibelleDC = libelleDC, description = description };
                    _context.DCs.Add(newDC);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Direction Centrale ajoutée avec succès.";
                }
            }
            return RedirectToAction(nameof(GestionDCs));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManageDCs)]
        // ✨ Add the description parameter
        public async Task<IActionResult> EditDC(string id, string libelle, string description)
        {
            var dc = await _context.DCs.FindAsync(id);
            if (dc != null && !string.IsNullOrEmpty(libelle))
            {
                dc.LibelleDC = libelle;
                // ✨ Add this line to update the description
                dc.description = description;
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "DC mise à jour avec succès." });
            }
            return BadRequest(new { success = false, message = "Erreur lors de la mise à jour." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = Permissions.ManageDCs)]
        public async Task<IActionResult> DeleteDC(string id)
        {
            var dc = await _context.DCs.FindAsync(id);
            if (dc != null)
            {
                // Safety Check: Ensure no users are assigned to this DC
                bool isInUse = await _context.Users.AnyAsync(u => u.CodeDIW == id);
                if (isInUse)
                {
                    TempData["ErrorMessage"] = "Impossible de supprimer cette DC car elle est assignée à un ou plusieurs utilisateurs.";
                }
                else
                {
                    _context.DCs.Remove(dc);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "DC supprimée avec succès.";
                }
            }
            return RedirectToAction(nameof(GestionDCs));
        }

        [HttpGet]
        public async Task<IActionResult> ChangePicture()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound();
            }
            // Assumes the view exists at /Views/Shared/ChangePicture.cshtml
            return View("~/Views/Shared/ChangePicture.cshtml", user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePicture(IFormFile picture)
        {
            if (picture == null || picture.Length == 0)
            {
                TempData["ErrorMessage"] = "Veuillez sélectionner un fichier image.";
                return RedirectToAction("ChangePicture");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            // Define the path to save the image
            string wwwRootPath = _hostEnvironment.WebRootPath;
            string fileName = Guid.NewGuid().ToString() + Path.GetExtension(picture.FileName);
            string uploadsFolder = Path.Combine(wwwRootPath, "uploads", "avatars");
            string filePath = Path.Combine(uploadsFolder, fileName);

            // Ensure the directory exists
            Directory.CreateDirectory(uploadsFolder);

            // Save the new picture
            await using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await picture.CopyToAsync(fileStream);
            }

            // Delete the old picture if it exists
            if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
            {
                var oldImagePath = Path.Combine(wwwRootPath, user.ProfilePictureUrl.TrimStart('/'));
                if (System.IO.File.Exists(oldImagePath))
                {
                    System.IO.File.Delete(oldImagePath);
                }
            }

            // Update the user's record in the database
            user.ProfilePictureUrl = $"/uploads/avatars/{fileName}";
            _context.Update(user);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Votre photo de profil a été mise à jour.";
            return RedirectToAction("Profile");
        }

        // In Controllers/AdminController.cs *****************************************************************************************************************************************************

        // ACTION 1: To display the list of pending situations
        [HttpGet]
         [Authorize(Policy = Permissions.ValidateSituations)] // Assuming a new permission
        public async Task<IActionResult> ListPendingSituations()
        {
            // 1. Get the list of User IDs for only DC and DRI roles.
            var adminValidatableUserIds = await _context.Users
                .Where(u => u.Statut == (int)UserRole.DRI || u.Statut == (int)UserRole.DC)
                .Select(u => u.ID_User)
                .ToListAsync();

            // 2. Update the query to only find pending situations created by those users.
            var pendingSituations = await _context.Situations
                .Where(s => s.Statut == 1 && adminValidatableUserIds.Contains(s.User_id))
                .OrderBy(s => s.ConfirmDate)
                .ToListAsync();

            // The rest of the method correctly formats the data for the view
            var viewModelData = new List<ValidatedSituationViewModel>();
            var users = await _context.Users.ToDictionaryAsync(u => u.ID_User);
            var dris = await _context.DRIs.ToDictionaryAsync(d => d.CodeDRI, d => d.LibelleDRI);
            var dcs = await _context.DCs.ToDictionaryAsync(d => d.CodeDC, d => d.LibelleDC);

            foreach (var situation in pendingSituations)
            {
                users.TryGetValue(situation.User_id, out var user);
                string situationType = "N/A", structureName = situation.DIW;

                if (user != null)
                {
                    switch ((UserRole)user.Statut)
                    {
                        case UserRole.DC:
                            situationType = "Stratégique";
                            if (dcs.TryGetValue(situation.DIW, out var dcName)) structureName = dcName;
                            break;
                        case UserRole.DRI:
                            situationType = "Opérationnelle (DRI)";
                            if (dris.TryGetValue(situation.DIW, out var driName)) structureName = driName;
                            break;
                    }
                }
                viewModelData.Add(new ValidatedSituationViewModel
                {
                    SituationId = situation.IDSituation,
                    Period = $"{situation.Month} {situation.Year}",
                    StructureName = structureName,
                    SubmittedBy = user != null ? $"{user.FirstNmUser} {user.LastNmUser}" : "Inconnu",
                    SituationType = situationType,
                    ValidationDate = situation.ConfirmDate
                });
            }
            return View(viewModelData);
        }

        // ACTION 2: To display the details of a single situation for review
        [HttpGet]
         [Authorize(Policy = Permissions.ValidateSituations)]
        public async Task<IActionResult> ReviewSituation(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var situation = await _context.Situations.Include(s => s.RejectionHistories).FirstOrDefaultAsync(s => s.IDSituation == id);
            if (situation == null) return NotFound();
            var user = await _context.Users.FindAsync(situation.User_id);
            if (user == null) return NotFound();

            ViewBag.Situation = situation;

            if (user.Statut == (int)UserRole.DC)
            {
                ViewBag.StructureName = (await _context.DCs.FindAsync(situation.DIW))?.LibelleDC;
                var declarations = await _context.DeclarationsStrategiques
                    .Where(d => d.IDSituation == id)
                    .Include(d => d.IndicateurStrategique.CategorieIndicateur)
                    .Include(d => d.IndicateurStrategique.Objectif)
                    .ToListAsync();

                // ==================== THIS BLOCK IS NOW CORRECTLY FILLED ====================
                var viewModel = new AdminConsulteStratViewModel
                {
                    Situation = situation, // This was the missing part
                    Axes = declarations
                        .GroupBy(d => d.IndicateurStrategique.CategorieIndicateur)
                        .Select(axeGroup => new AxeViewModel
                        {
                            AxeName = axeGroup.Key.IntituleCategIn,
                            Objectifs = axeGroup
                                .GroupBy(d => d.IndicateurStrategique.Objectif)
                                .Select(objGroup => new ObjectifViewModel
                                {
                                    ObjectifName = objGroup.Key.Intituleobj,
                                    Declarations = objGroup.ToList()
                                }).ToList()
                        }).ToList()
                };
                // ==========================================================================
                ViewBag.CurrentController = "Admin";
                return View("ReviewStrat", viewModel);
            }

            bool isDriSelfReport = await _context.DeclarationDRIs.AnyAsync(d => d.IDSituation == id);
            if (isDriSelfReport)
            {
                ViewBag.StructureName = (await _context.DRIs.FindAsync(situation.DIW))?.LibelleDRI;
                var declarations = await _context.DeclarationDRIs.Where(d => d.IDSituation == id).Include(d => d.Indicateur).ToListAsync();
                var viewModel = new AdminConsulteDriViewModel { Situation = situation, Declarations = declarations };
                return View("ReviewDRI", viewModel);
            }

            return BadRequest("Type de situation inconnu pour la révision.");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        //[Authorize(Policy = Permissions.ValidateSituations)]
        public async Task<IActionResult> ValidateSituation(string id)
        {
            var situation = await _context.Situations.FindAsync(id);
            if (situation == null || situation.Statut != 1) return BadRequest();

            situation.Statut = 3; // 3 = Validé

            // ✨ ADD THIS LINE to record the timestamp of the admin's validation
            situation.AdminValidationDate = DateTime.Now;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Situation validée avec succès.";
            return RedirectToAction("ListPendingSituations");
        }

        // ACTION 4: To handle the "Reject" button click
        [HttpPost]
        [ValidateAntiForgeryToken]
         [Authorize(Policy = Permissions.ValidateSituations)]
        public async Task<IActionResult> RejectSituation(string id, string comment)
        {
            if (string.IsNullOrWhiteSpace(comment))
            {
                TempData["ErrorMessage"] = "Le motif du rejet est obligatoire.";
                return RedirectToAction("ReviewSituation", new { id });
            }

            var situation = await _context.Situations.FindAsync(id);
            if (situation == null || situation.Statut != 1) return BadRequest();

            var user = await _context.Users.FindAsync(situation.User_id);
            if (user == null) return NotFound();

            if (user.Statut == (int)UserRole.DC) // Handle DC Situation Rejection
            {
                var declarations = await _context.DeclarationsStrategiques.Where(d => d.IDSituation == id).ToListAsync();
                foreach (var d in declarations)
                {
                    // Correctly copy all properties to a new draft
                    _context.DeclarationsStrategiquesDrafts.Add(new DeclarationStrategiqueDraft
                    {
                        IDSituation = d.IDSituation,
                        IdIndic = d.IdIndic,
                        Numerateur = d.Numerateur,
                        Denominateur = d.Denominateur,
                        Taux = d.taux,
                        Ecart = d.ecart,
                        Cible = d.Cible
                    });
                }
                _context.DeclarationsStrategiques.RemoveRange(declarations);
            }
            else // Handle DRI Situation Rejection
            {
                var declarations = await _context.DeclarationDRIs.Where(d => d.IDSituation == id).ToListAsync();
                foreach (var d in declarations)
                {
                    // Correctly copy all properties to a new draft
                    _context.DeclarationDRIDrafts.Add(new DeclarationDRIDraft
                    {
                        IDSituation = d.IDSituation,
                        IdIndicacteur = d.IdIndicacteur,
                        Numerateur = d.Numerateur,
                        Denominateur = d.Denominateur,
                        taux = d.taux,
                        ecart = d.ecart,
                        Cible = d.Cible
                    });
                }
                _context.DeclarationDRIs.RemoveRange(declarations);
            }

            situation.Statut = 2; // 2 = Rejeté
            situation.EditDate = DateTime.Now;
            _context.RejectionHistories.Add(new RejectionHistory
            {
                IDSituation = id,
                Comment = comment,
                RejectionDate = DateTime.Now,
                RejectedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            });

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Situation rejetée et renvoyée avec succès.";
            return RedirectToAction("ListPendingSituations");
        }

        // --- ADD TO AdminController.cs ---

        [HttpGet] // Changed from HttpPost to HttpGet
                  // [ValidateAntiForgeryToken] REMOVED
        public async Task<IActionResult> ExportNiveauOperationnel(NiveauOpViewModel filters)
        {
            // The logic inside remains exactly the same as before
            if (string.IsNullOrEmpty(filters.SelectedDri)) return RedirectToAction("NiveauOperationnel");

            var diwOptions = await _context.DIWs
                .Where(d => d.CodeDRI == filters.SelectedDri)
                .Select(d => new SelectListItem { Value = d.CodeDIW })
                .ToListAsync();

            IQueryable<Declaration> query = _context.Declarations
                .Include(d => d.Situation)
                .Include(d => d.Indicateur)
                .ThenInclude(i => i.CategorieIndicateur);

            if (!string.IsNullOrEmpty(filters.SelectedYear)) query = query.Where(d => d.Situation.Year == filters.SelectedYear);
            if (!string.IsNullOrEmpty(filters.SelectedAxe)) query = query.Where(d => d.Indicateur.IdCategIn == filters.SelectedAxe);
            if (!string.IsNullOrEmpty(filters.SelectedIndicateur)) query = query.Where(d => d.IdIn == filters.SelectedIndicateur);

            if (!string.IsNullOrEmpty(filters.SelectedDiw))
            {
                query = query.Where(d => d.Situation.DIW == filters.SelectedDiw);
            }
            else if (!string.IsNullOrEmpty(filters.SelectedDri))
            {
                var diwCodesInDri = diwOptions.Select(d => d.Value).ToList();
                query = query.Where(d => diwCodesInDri.Contains(d.Situation.DIW));
            }

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
                    double taux = (totalDenominateur > 0) ? (totalNumerateur / totalDenominateur) * 100 : 0;
                    double cible = g.Average(d => d.Cible ?? 0);
                    return new IndicatorPerformanceViewModel
                    {
                        AxeName = g.Key.CategorieIndicateur.IntituleCategIn,
                        IndicatorName = g.Key.IntituleIn,
                        SumNumerateur = totalNumerateur,
                        SumDenominateur = totalDenominateur,
                        Taux = taux,
                        Cible = cible,
                        Ecart = taux - cible
                    };
                })
                .OrderBy(r => r.AxeName).ThenBy(r => r.IndicatorName)
                .ToList();

            var (fileContents, fileName) = await _reportService.GenerateAnalysisOpExcelAsync(results, filters);
            return File(fileContents, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [HttpGet] // Changed from HttpPost to HttpGet
                  // [ValidateAntiForgeryToken] REMOVED
        public async Task<IActionResult> ExportNiveauStrategique(NiveauStratViewModel filters)
        {
            // Logic remains exactly the same
            IQueryable<DeclarationStrategique> query = _context.DeclarationsStrategiques
                .Include(d => d.Situation)
                .Include(d => d.IndicateurStrategique).ThenInclude(i => i.CategorieIndicateur)
                .Include(d => d.IndicateurStrategique).ThenInclude(i => i.Objectif);

            bool isAnyFilterApplied = !string.IsNullOrEmpty(filters.SelectedYear) || filters.SelectedSemester.HasValue || filters.SelectedTrimester.HasValue || filters.SelectedMonth.HasValue || !string.IsNullOrEmpty(filters.SelectedAxe) || filters.SelectedObjectif.HasValue;

            if (isAnyFilterApplied)
            {
                if (!string.IsNullOrEmpty(filters.SelectedYear)) query = query.Where(d => d.Situation.Year == filters.SelectedYear);
                if (!string.IsNullOrEmpty(filters.SelectedAxe)) query = query.Where(d => d.IndicateurStrategique.IdCategIn == filters.SelectedAxe);
                if (filters.SelectedObjectif.HasValue) query = query.Where(d => d.IndicateurStrategique.idobj == filters.SelectedObjectif.Value);

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
            }
            else
            {
                var dcUserIds = await _context.Users.Where(u => u.Statut == 2).Select(u => u.ID_User).ToListAsync();
                var lastValidatedDcSituationIds = await _context.Situations.Where(s => s.Statut == 3 && dcUserIds.Contains(s.User_id)).GroupBy(s => s.User_id).Select(g => g.OrderByDescending(s => s.ConfirmDate).First().IDSituation).ToListAsync();
                query = query.Where(d => lastValidatedDcSituationIds.Contains(d.IDSituation));
            }

            var aggregatedData = await query
                .GroupBy(d => new { IndicatorName = d.IndicateurStrategique.IntituleIn, AxeName = d.IndicateurStrategique.CategorieIndicateur.IntituleCategIn, ObjectifName = d.IndicateurStrategique.Objectif.Intituleobj })
                .Select(g => new { g.Key.AxeName, g.Key.ObjectifName, g.Key.IndicatorName, AverageCible = g.Average(d => d.Cible), TotalNumerateur = g.Sum(d => d.Numerateur), TotalDenominateur = g.Sum(d => d.Denominateur) })
                .OrderBy(r => r.AxeName).ThenBy(r => r.ObjectifName).ThenBy(r => r.IndicatorName)
                .ToListAsync();

            var results = aggregatedData.Select(r => new IndicatorStratPerformanceViewModel
            {
                AxeName = r.AxeName,
                ObjectifName = r.ObjectifName,
                IndicatorName = r.IndicatorName,
                SumNumerateur = (double)(r.TotalNumerateur ?? 0),
                SumDenominateur = (double)(r.TotalDenominateur ?? 0),
                Taux = (r.TotalDenominateur > 0) ? ((double)r.TotalNumerateur / (double)r.TotalDenominateur) * 100 : 0,
                Cible = r.AverageCible ?? 0,
                Ecart = ((r.TotalDenominateur > 0) ? ((double)r.TotalNumerateur / (double)r.TotalDenominateur) * 100 : 0) - (r.AverageCible ?? 0)
            }).ToList();

            var (fileContents, fileName) = await _reportService.GenerateAnalysisStratExcelAsync(results, filters);
            return File(fileContents, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        // File: Controllers/AdminController.cs

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ConsulteRapports(string filterStructure, string filterYear)
        {
            // 1. Fetch Data
            var query = _context.Rapports.Include(r => r.User).AsQueryable();

            // 2. Apply Filters
            if (!string.IsNullOrEmpty(filterYear))
            {
                query = query.Where(r => r.Year == filterYear);
            }
            if (!string.IsNullOrEmpty(filterStructure))
            {
                query = query.Where(r => r.CodeStructure == filterStructure);
            }

            // 3. Get List ordered by date
            var reports = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();

            // 4. Resolve Names (DIW/DRI/DC)
            var allStructures = new Dictionary<string, string>();
            var diws = await _context.DIWs.ToListAsync();
            var dris = await _context.DRIs.ToListAsync();
            var dcs = await _context.DCs.ToListAsync();

            diws.ForEach(d => allStructures[d.CodeDIW] = $"DIW {d.LibelleDIW}");
            dris.ForEach(d => allStructures[d.CodeDRI] = $"DRI {d.LibelleDRI}");
            dcs.ForEach(d => allStructures[d.CodeDC] = $"DC {d.LibelleDC}");

            ViewBag.StructureNames = allStructures;

            // Pass lists for filter dropdowns
            ViewBag.Years = await _context.Rapports.Select(r => r.Year).Distinct().ToListAsync();
            ViewBag.Structures = allStructures;

            return View(reports);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ValiderRapport(int id)
        {
            var rapport = await _context.Rapports.FindAsync(id);
            if (rapport == null) return NotFound();

            rapport.Status = 1; // Validated
            rapport.Motif = null;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Rapport validé avec succès.";
            return RedirectToAction(nameof(ConsulteRapports));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejeterRapport(int id, string motif)
        {
            var rapport = await _context.Rapports.FindAsync(id);
            if (rapport == null) return NotFound();

            rapport.Status = 2; // Rejected
            rapport.Motif = motif;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Le rapport a été rejeté.";
            return RedirectToAction(nameof(ConsulteRapports));
        }

        // --- GESTION DES INDICATEURS DE PERFORMANCE (DRI) ---

        [HttpGet]
        public async Task<IActionResult> ListIndicateursDri()
        {
            var indicators = await _context.Indicateurs_DE_PERFORMANCE_OPERATIONNELS
                .OrderBy(i => i.IdIndicacteur)
                .ToListAsync();
            return View(indicators);
        }

        [HttpGet]
        public async Task<IActionResult> AddIndicateurDri()
        {
            ViewBag.Categories = await _context.CategorieIndicateurs
                .OrderBy(c => c.IntituleCategIn)
                .Select(c => new SelectListItem { Value = c.IdCategIn, Text = c.IntituleCategIn })
                .ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddIndicateurDri(Indicateurs_DE_PERFORMANCE_OPERATIONNELS model)
        {
            if (ModelState.IsValid)
            {
                _context.Indicateurs_DE_PERFORMANCE_OPERATIONNELS.Add(model);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Indicateur de performance ajouté avec succès.";
                return RedirectToAction(nameof(ListIndicateursDri));
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> EditIndicateurDri(int id)
        {
            var indicateur = await _context.Indicateurs_DE_PERFORMANCE_OPERATIONNELS.FindAsync(id);
            if (indicateur == null) return NotFound();

            ViewBag.Categories = await _context.CategorieIndicateurs
                .OrderBy(c => c.IntituleCategIn)
                .Select(c => new SelectListItem { Value = c.IdCategIn, Text = c.IntituleCategIn })
                .ToListAsync();

            return View(indicateur);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditIndicateurDri(Indicateurs_DE_PERFORMANCE_OPERATIONNELS model)
        {
            if (ModelState.IsValid)
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Indicateur mis à jour avec succès.";
                return RedirectToAction(nameof(ListIndicateursDri));
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteIndicateurDri(int id)
        {
            var indicateur = await _context.Indicateurs_DE_PERFORMANCE_OPERATIONNELS.FindAsync(id);
            if (indicateur == null) return NotFound();

            // Optionnel : Vérifier si des cibles sont liées avant suppression
            var hasTargets = await _context.cibles_de_performance_dri.AnyAsync(c => c.IdIndicacteur == id);
            if (hasTargets)
            {
                _context.cibles_de_performance_dri.RemoveRange(_context.cibles_de_performance_dri.Where(c => c.IdIndicacteur == id));
            }

            _context.Indicateurs_DE_PERFORMANCE_OPERATIONNELS.Remove(indicateur);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Indicateur supprimé.";
            return RedirectToAction(nameof(ListIndicateursDri));
        }

    }
}