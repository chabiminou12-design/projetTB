using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Stat.Helpers;
using Stat.Models;
using Stat.Models.Enums;
using Stat.Models.ViewModels;
using System.Security.Claims;

namespace Stat.Controllers
{
    [Authorize(Policy = "SuperAdminOnly")]
    public class SuperAdminController : Controller
    {
        private readonly DatabaseContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;


        public SuperAdminController(DatabaseContext context, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
        }

        // This action lists all regular (non-Super Admin) administrators
        // Replace the Index method in SuperAdminController.cs

        [Authorize(Policy = "SuperAdminOnly")]
        public async Task<IActionResult> Index()
        {
            // 1. Fetch Base Data (AsNoTracking for performance)
            var users = await _context.Users.AsNoTracking().ToListAsync();
            var situations = await _context.Situations.AsNoTracking().ToListAsync();

            // 2. Calculate Rich Statistics
            int totalDocs = situations.Count;
            int validatedDocs = situations.Count(s => s.Statut == 3);
            double completionRate = totalDocs > 0 ? ((double)validatedDocs / totalDocs) * 100 : 0;
            ViewBag.RoleNames = new Dictionary<int, string>
    {
        { 0, "USER_DIW" }, { 1, "USER_DRI" }, { 2, "USER_DC" }, { 3, "USER_Admin" }, { 4, "USER_Directeur" }
    };
            var structureNames = new Dictionary<string, string>();
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
            var viewModel = new SuperAdminDashboardViewModel
            {
                // Basic Counts
                TotalUsers = users.Count,
                ActiveUserCount = users.Count(u => u.IsActive),
                InactiveUserCount = users.Count(u => !u.IsActive),

                // Role Breakdown
                TotalAdmins = users.Count(u => u.Statut == (int)UserRole.Admin),
                TotalDirecteurs = users.Count(u => u.Statut == (int)UserRole.Director),
                TotalDcs = users.Count(u => u.Statut == (int)UserRole.DC),
                TotalDris = users.Count(u => u.Statut == (int)UserRole.DRI),
                TotalDiws = users.Count(u => u.Statut == (int)UserRole.DIW),

                // System Health
                TotalSituationsSubmitted = totalDocs,
                TotalSituationsValidated = validatedDocs,
                SystemCompletionRate = Math.Round(completionRate, 1),

                // Activity Feeds (Rich Data)
                RecentlyCreatedUsers = users.OrderByDescending(u => u.DateDeCreation).Take(5).ToList(),
                RecentlyConnectedUsers = users.Where(u => u.LastCnx.HasValue)
                                              .OrderByDescending(u => u.LastCnx)
                                              .Take(5)
                                              .ToList()
            };

            return View(viewModel);
        }

        // GET action to display the permissions checklist for a specific admin
        [HttpGet]
        public async Task<IActionResult> ManagePermissions(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var admin = await _context.Users.FindAsync(id);
            if (admin == null || admin.Statut != (int)UserRole.Admin) return NotFound();

            var allPermissions = await _context.Permissions.OrderBy(p => p.PermissionId).ToListAsync();
            var adminPermissions = await _context.UserPermissions
                .Where(up => up.UserId == id)
                .Select(up => up.PermissionId)
                .ToListAsync();

            var viewModel = new ManagePermissionsViewModel
            {
                AdminId = admin.ID_User,
                AdminName = $"{admin.FirstNmUser} {admin.LastNmUser}",
                AssignedPermissions = allPermissions.Select(p => new PermissionAssignmentViewModel
                {
                    PermissionId = p.PermissionId,
                    Description = p.Description,
                    IsAssigned = adminPermissions.Contains(p.PermissionId)
                }).ToList()
            };

            return View(viewModel);
        }

        // POST action to save the changes made to an admin's permissions
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManagePermissions(ManagePermissionsViewModel viewModel)
        {
            // 1. Récupérer l'état actuel des permissions de l'administrateur
            var currentPermissions = await _context.UserPermissions
                .Where(up => up.UserId == viewModel.AdminId)
                .ToListAsync();

            // 2. Déterminer les permissions souhaitées (cochées) et les stocker dans un HashSet de STRING
            var assignedPermissionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (viewModel.AssignedPermissions != null)
            {
                assignedPermissionIds = viewModel.AssignedPermissions
                    .Where(p => p.IsAssigned)
                    .Select(p => p.PermissionId)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase); // Utilisation de StringComparer pour être robuste
            }

            // --- Gestion des Suppressions (DELETE) ---
            // Les permissions à supprimer sont celles qui existent DANS la DB
            // mais ne sont PAS cochées dans le formulaire (viewModel).
            var permissionsToRemove = currentPermissions
                .Where(up => !assignedPermissionIds.Contains(up.PermissionId))
                .ToList();

            _context.UserPermissions.RemoveRange(permissionsToRemove);

            // --- Gestion des Ajouts (INSERT) ---
            // Récupérer les ID existants pour ne pas les réinsérer.
            var currentPermissionIds = currentPermissions.Select(up => up.PermissionId).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Les permissions à ajouter sont celles qui sont cochées dans le formulaire 
            // mais n'existent PAS encore dans la DB.
            var permissionsToAdd = assignedPermissionIds
                .Where(id => !currentPermissionIds.Contains(id))
                .Select(id => new UserPermission
                {
                    UserId = viewModel.AdminId,
                    PermissionId = id // id est bien un string
                })
                .ToList();

            _context.UserPermissions.AddRange(permissionsToAdd);

            // 5. Sauvegarder les changements (suppressions et additions) en une seule transaction
            // SEULS les ajouts et les suppressions nécessaires sont envoyés à la DB.
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Les permissions pour {viewModel.AdminName} ont été mises à jour.";

            // IMPORTANT : Recharger le modèle pour que la vue affiche le nouvel état
            var allPermissions = await _context.Permissions.OrderBy(p => p.PermissionId).ToListAsync();

            // Récupérer les IDs finaux après la sauvegarde
            var finalAssignedIdsList = await _context.UserPermissions
                                         .Where(up => up.UserId == viewModel.AdminId)
                                         .Select(up => up.PermissionId)
                                         .ToListAsync(); // <-- Utiliser ToListAsync()

            var finalAssignedIds = finalAssignedIdsList.ToHashSet(StringComparer.OrdinalIgnoreCase); // <-- Puis ToHashSet()

            viewModel.AssignedPermissions = allPermissions.Select(p => new PermissionAssignmentViewModel
            {
                PermissionId = p.PermissionId,
                Description = p.Description,
                IsAssigned = finalAssignedIds.Contains(p.PermissionId)
            }).ToList();

            return View(viewModel);
        }
        public async Task<IActionResult> ListAllUsers(string searchString, int? roleFilter, int? pageNumber)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentRoleFilter"] = roleFilter;

            // 1. Définir la requête de base
            var usersQuery = _context.Users.AsQueryable();

            // 2. Appliquer le filtre de recherche
            if (!string.IsNullOrEmpty(searchString))
            {
                usersQuery = usersQuery.Where(u => u.LastNmUser.Contains(searchString)
                                               || u.FirstNmUser.Contains(searchString)
                                               || u.User_name.Contains(searchString));
            }

            // 3. Appliquer le filtre de rôle
            if (roleFilter.HasValue)
            {
                usersQuery = usersQuery.Where(u => u.Statut == roleFilter.Value);
            }

            // 4. Définir le ViewBag pour les Rôles
            ViewBag.RoleNames = new Dictionary<int, string>
    {
        { 0, "USER_DIW" }, { 1, "USER_DRI" }, { 2, "USER_DC" }, { 3, "USER_Admin" }, { 4, "USER_Directeur" }
    };

            // 5. NOUVEAU: Créer un dictionnaire pour les noms de structure
            var structureNames = new Dictionary<string, string>();

            // Charger les DIWs
            var diws = await _context.DIWs.AsNoTracking().ToListAsync();
            foreach (var diw in diws)
            {
                if (diw.CodeDIW != null) structureNames[diw.CodeDIW] = "DIW-"+diw.LibelleDIW;
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


            // 6. Paginer les résultats
            var paginatedUsers = await PaginatedList<User>.CreateAsync(
                usersQuery.OrderByDescending(u => u.DateDeCreation),
                pageNumber ?? 1,
                15); // Show 15 users per page

            return View(paginatedUsers);
        }

        // Add these new methods inside your SuperAdminController

        // GET: /SuperAdmin/AddUser
        // Displays the page to choose which type of user to create
        [HttpGet]
        public IActionResult AddUser()
        {
            return View();
        }

        // GET: /SuperAdmin/AddUserForm
        // Prepares the form for the selected user type
        [HttpGet]
        public async Task<IActionResult> AddUserForm(string userType)
        {
            if (string.IsNullOrEmpty(userType)) return RedirectToAction("AddUser");

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
                             .ToListAsync(),
                AllDiws = await _context.DIWs.OrderBy(w => w.LibelleDIW).ToListAsync()
            };

            return View(viewModel);
        }

        // POST: /SuperAdmin/AddUserForm
        // Processes the creation of the new user
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddUserForm(AddUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // If the model is invalid, you must repopulate ALL lists needed by the view
                model.DRIOptions = await _context.DRIs
                    .OrderBy(d => d.LibelleDRI)
                    .Select(d => new SelectListItem { Value = d.CodeDRI, Text = d.LibelleDRI })
                    .ToListAsync();
                model.DCOptions = await _context.DCs.OrderBy(d => d.LibelleDC)
                    .Select(d => new SelectListItem { Value = d.CodeDC, Text = d.LibelleDC })
                    .ToListAsync();
                model.AllDiws = await _context.DIWs.OrderBy(w => w.LibelleDIW).ToListAsync();

                // Return the view with the model so the user can correct their errors
                return View(model);
            }

            string codeStructure = "";
            int userRoleStatut = 0;
            switch (model.UserType)
            {
                case "DIW": userRoleStatut = (int)UserRole.DIW; codeStructure = model.SelectedDIW; break;
                case "DRI": userRoleStatut = (int)UserRole.DRI; codeStructure = model.SelectedDRI; break;
                case "DC": codeStructure = model.SelectedDC; userRoleStatut = 2; break;
                case "Admin": userRoleStatut = (int)UserRole.Admin; codeStructure = "1600A00"; break;
                case "Director":
                    userRoleStatut = (int)UserRole.Director; codeStructure = model.SelectedDirectorStructure;
                    if (string.IsNullOrEmpty(codeStructure))
                    {
                        ModelState.AddModelError("SelectedDirectorStructure", "Un directeur doit être assigné à une structure.");
                        return View(model);
                    }
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
                ID_User = Guid.NewGuid().ToString(), // A GUID is a safer unique ID
                User_name = $"{model.FirstNmUser.Substring(0, 1).ToLower()}.{model.LastNmUser.ToLower()}",
                FirstNmUser = model.FirstNmUser,
                LastNmUser = model.LastNmUser,
                MailUser = model.MailUser,
                TelUser = model.TelUser,
                CodeDIW = codeStructure,
                Date_deb_Affect = model.Date_deb_Affect,
                motdepasse = plainTextPassword,
                Password = AccessController.sha256_hash(plainTextPassword),
                Statut = userRoleStatut,
                IsActive = false,
                DateDeCreation = DateTime.Now,
                CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            };
            
            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // ✨ If the new user is an Admin, save their assigned permissions
            if (model.UserType == "Admin" && model.PermissionsToAssign.Any())
            {
                foreach (var permission in model.PermissionsToAssign.Where(p => p.IsAssigned))
                {
                    _context.UserPermissions.Add(new UserPermission
                    {
                        UserId = newUser.ID_User,
                        PermissionId = permission.PermissionId
                    });
                }
                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] =
$"Utilisateur '{newUser.FirstNmUser} {newUser.LastNmUser}' créé avec succès. " +
$"Mot de passe:( {plainTextPassword} )." +
"<span style='font-weight: bold; color: red;'>IMPORTANT :</span> " + // Fixed the concatenation and span
"Le compte est désactivé par défaut. Activation immédiate nécessaire pour la connexion.";
            return RedirectToAction("ListAllUsers");
        }

        // Add this new method anywhere inside your SuperAdminController class

        [Authorize(Policy = "SuperAdminOnly")]
        public async Task<IActionResult> ListAdmins()
        {
            var admins = await _context.Users
                .Where(u => u.Statut == (int)UserRole.Admin)
                .OrderBy(u => u.User_name)
                .ToListAsync();

            // We pass the Super Admin's ID to the view to exclude them from the list
            var superAdminSetting = await _context.AppSettings.FindAsync("SuperAdminUserId");
            ViewBag.SuperAdminId = superAdminSetting?.Value;

            return View(admins);
        }
        // Add this new method anywhere inside your SuperAdminController class

        [HttpGet]
        [Authorize(Policy = "SuperAdminOnly")]
        public async Task<IActionResult> GetDiwsByDri(string driCode)
        {
            var diws = await _context.DIWs
                                 .Where(d => d.CodeDRI == driCode)
                                 .OrderBy(d => d.LibelleDIW)
                                 .Select(d => new { value = d.CodeDIW, text = d.LibelleDIW })
                                 .ToListAsync();
            return Json(diws);
        }
        // Add these new methods inside your SuperAdminController class

        // GET: /SuperAdmin/EditUser/some-id
        [HttpGet]
        public async Task<IActionResult> EditUser(string id)
        {
            if (id == null) return NotFound();
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            var viewModel = new EditUserViewModel
            {
                User = user,
                // For DIW/DRI selection dropdowns
                DRIOptions = await _context.DRIs.OrderBy(d => d.LibelleDRI).Select(d => new SelectListItem { Value = d.CodeDRI, Text = d.LibelleDRI }).ToListAsync(),
                // For Role selection dropdown
                StatutOptions = new List<SelectListItem>
        {
            new SelectListItem { Value = "0", Text = "Utilisateur DIW" },
            new SelectListItem { Value = "1", Text = "Utilisateur DRI" },
            new SelectListItem { Value = "2", Text = "Utilisateur DC" },
            new SelectListItem { Value = "3", Text = "Administrateur" },
            new SelectListItem { Value = "4", Text = "Utilisateur directeur" }
        }
            };
            return View(viewModel);
        }

        // POST: /SuperAdmin/EditUser/some-id
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(string id, EditUserViewModel viewModel)
        {
            if (id != viewModel.User.ID_User) return NotFound();

            if (ModelState.IsValid)
            {
                var userToUpdate = await _context.Users.FindAsync(id);
                if (userToUpdate == null) return NotFound();

                // Update properties from the form
                userToUpdate.User_name = viewModel.User.User_name;
                userToUpdate.FirstNmUser = viewModel.User.FirstNmUser;
                userToUpdate.LastNmUser = viewModel.User.LastNmUser;
                userToUpdate.MailUser = viewModel.User.MailUser;
                userToUpdate.TelUser = viewModel.User.TelUser;
                userToUpdate.Statut = viewModel.User.Statut;
                userToUpdate.CodeDIW = viewModel.User.CodeDIW;

                _context.Update(userToUpdate);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Utilisateur mis à jour avec succès.";
                return RedirectToAction("EditUser");
            }

            // If model state is invalid, re-populate dropdowns and return to the view
            viewModel.DRIOptions = await _context.DRIs.OrderBy(d => d.LibelleDRI).Select(d => new SelectListItem { Value = d.CodeDRI, Text = d.LibelleDRI }).ToListAsync();
      
            return View(viewModel);
        }

        // Add this new method anywhere inside your SuperAdminController class

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleIsActive(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            // Safety Check: Prevent the Super Admin from deactivating their own account
            var superAdminSetting = await _context.AppSettings.FindAsync("SuperAdminUserId");
            if (superAdminSetting?.Value == id)
            {
                return BadRequest(new { success = false, message = "Action non autorisée. Le Super Administrateur ne peut pas désactiver son propre compte." });
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Toggle the activation status
            user.IsActive = !user.IsActive;

            _context.Update(user);
            await _context.SaveChangesAsync();

            // Return the new status to the front-end
            return Ok(new { success = true, newStatus = user.IsActive });
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
            return View(user);
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
            return RedirectToAction("ChangePicture");
        }

        public async Task<IActionResult> Profile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            string roleName = "";

            if (user == null)
            {
                return NotFound();
            }

            var viewModel = new ProfileViewModel
            {
                UserProfile = user,
                RoleName = "Super Administrateur",

            };
            return View("~/Views/SuperAdmin/Profile.cshtml", viewModel);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<JsonResult> UpdateProfileInfo(string email, string phone)
        {
            // The 'username' parameter has been removed
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(phone))
            {
                return Json(new { success = false, message = "L'email et le téléphone sont requis." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userToUpdate = await _context.Users.FindAsync(userId);
            if (userToUpdate == null) return Json(new { success = false, message = "Utilisateur non trouvé." });

            // The username is no longer updated
            // userToUpdate.User_name = username;
            userToUpdate.MailUser = email;
            userToUpdate.TelUser = phone;

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Informations mises à jour avec succès." });
        }
    }
}