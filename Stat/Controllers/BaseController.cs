using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stat.Models;
using Stat.Models.ViewModels;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Stat.Controllers
{
    [Authorize]
    public abstract class BaseController : Controller
    {
        protected readonly DatabaseContext _context;
        protected readonly IWebHostEnvironment _hostEnvironment;

        public BaseController(DatabaseContext context, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadRapport(IFormFile rapportFile, string type, string year)
        {
            if (rapportFile == null || rapportFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Erreur : Veuillez sélectionner un fichier.";
                return RedirectToAction("Index");
            }

            var extension = Path.GetExtension(rapportFile.FileName).ToLower();
            var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
            if (!allowedExtensions.Contains(extension))
            {
                TempData["ErrorMessage"] = "Erreur : Seuls les fichiers PDF, DOC et DOCX sont autorisés.";
                return RedirectToAction("Index");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userStructureCode = User.FindFirstValue("CodeDIW");

            string fileName = $"{type}_{userStructureCode}_{year}_{Guid.NewGuid()}{extension}"; // Add Guid to avoid caching
            string path = Path.Combine(_hostEnvironment.WebRootPath, "uploads", "rapports", fileName);

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = new FileStream(path, FileMode.Create))
            {
                await rapportFile.CopyToAsync(stream);
            }

            // Logic: Always create a NEW record for history, OR update if you want single record.
            // Based on "Admin list should show latest", creating new is safer for history, 
            // but updating is cleaner for database size. Let's UPDATE existing if rejected/pending.

            var existingReport = await _context.Rapports
                .FirstOrDefaultAsync(r => r.CodeStructure == userStructureCode && r.Type == type && r.Year == year);

            if (existingReport != null)
            {
                existingReport.FilePath = $"/uploads/rapports/{fileName}";
                existingReport.Status = 0; // Reset to Pending
                existingReport.Motif = null; // Clear rejection motif
                existingReport.CreatedAt = DateTime.Now; // Update timestamp
                _context.Update(existingReport);
            }
            else
            {
                var newRapport = new Rapport
                {
                    User_id = userId,
                    CodeStructure = userStructureCode,
                    Type = type,
                    Year = year,
                    FilePath = $"/uploads/rapports/{fileName}",
                    Status = 0,
                    CreatedAt = DateTime.Now
                };
                _context.Rapports.Add(newRapport);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Rapport envoyé avec succès.";
            return RedirectToAction("Index");
        }
        // ✨ UPDATED: Shared Profile Page Logic
        public virtual async Task<IActionResult> Profile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            string roleName = "", structureName = "", parentStructureName = "";
            switch (user.Statut)
            {
                case (int)Stat.Models.Enums.UserRole.DIW:
                    var diw = await _context.DIWs.Include(d => d.DRI).FirstOrDefaultAsync(d => d.CodeDIW == user.CodeDIW);
                    roleName = "Utilisateur DIW";
                    structureName = diw?.LibelleDIW;
                    parentStructureName = diw?.DRI?.LibelleDRI; // This fetches the parent DRI name
                    break;
                case (int)Stat.Models.Enums.UserRole.DRI:
                    var dri = await _context.DRIs.FindAsync(user.CodeDIW);
                    roleName = "Utilisateur DRI";
                    structureName = "DRI " + dri?.LibelleDRI;
                    break;
                case (int)Stat.Models.Enums.UserRole.DC:
                    var dc = await _context.DCs.FindAsync(user.CodeDIW);
                    roleName = "Utilisateur DC";
                    structureName = "DC " + dc?.LibelleDC;
                    break;
                case (int)Stat.Models.Enums.UserRole.Admin:
                    roleName = "Administrateur";
                    structureName = "Accès Global";
                    break;
                case (int)Stat.Models.Enums.UserRole.Director:
                    roleName = "Directeur";
                    var dc1 = await _context.DCs.FindAsync(user.CodeDIW);
                    if (dc1 != null)
                    {
                        structureName = dc1.LibelleDC;
                        break; 
                    }

                    var dri1 = await _context.DRIs.FindAsync(user.CodeDIW);
                    if (dri1 != null)
                    {
                        structureName = "DRI "+ dri1.LibelleDRI;
                        break;
                    }

                    var diw1 = await _context.DIWs.Include(d => d.DRI).FirstOrDefaultAsync(d => d.CodeDIW == user.CodeDIW);
                    if (diw1 != null)
                    {
                        structureName = diw1.LibelleDIW;
                        parentStructureName = diw1.DRI?.LibelleDRI;
                        break;
                    }
                    structureName = "Directeur";
                    break;
            }

            var viewModel = new ProfileViewModel
            {
                UserProfile = user,
                RoleName = roleName,
                StructureName = structureName,
                ParentStructureName = parentStructureName
            };

            return View("~/Views/Shared/Profile.cshtml", viewModel);
        }

        // Shared Change Picture Logic (remains unchanged)
        [HttpGet]
        public async Task<IActionResult> ChangePicture()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound();
            }
            // We need a view for this, let's call it ChangePicture.cshtml
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

        // ✨ UPDATED: Shared AJAX update action for the Profile Page
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