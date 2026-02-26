using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stat.Models;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using System.Security.Cryptography;
using Stat.Models.Enums;
using DocumentFormat.OpenXml.Bibliography;

namespace Stat.Controllers
{
    public class AccessController : Controller
    {
        private readonly DatabaseContext _context;
        public AccessController(DatabaseContext context)
        {
            _context = context;
        }

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

        public IActionResult Login()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userRole = User.FindFirstValue(ClaimTypes.Role);

                // ✨ Add redirect for Super Admin
                if (User.HasClaim("IsSuperAdmin", "true"))
                {
                    return RedirectToAction("Index", "SuperAdmin");
                }

                switch (userRole)
                {
                    case "DIW": return RedirectToAction("Index", "DIW");
                    case "DRI": return RedirectToAction("Index", "DRI");
                    case "DC": return RedirectToAction("Index", "DC");
                    case "Director": return RedirectToAction("Index", "Director");    
                    case "Admin": return RedirectToAction("Index", "Admin");
                    default: return RedirectToAction("LogOut");
                }
            }
            return View();
        }

        // [Fichier: Controllers/AccessController.cs]

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(User model)
        {
            if (!string.IsNullOrEmpty(Request.Form["Confirm_Email_Address"]))
            {
                // Return to view as if nothing happened, or return a fake success.
                // This wastes the bot's time without using your database resources.
                return View(model);
            }
            var user = await _context.Users
                     .Include(u => u.UserPermissions)
                     .FirstOrDefaultAsync(u => u.User_name == model.User_name || u.MailUser == model.User_name);

            if (user != null && user.Password == sha256_hash(model.Password))
            {
                if (!user.IsActive)
                {
                    ViewData["ValidateMessage"] = "Votre compte est désactivé. Veuillez contacter l'administrateur.";
                    return View(model);
                }

                // --- NOUVELLE LOGIQUE DE SESSION ---
                // 1. Générer un nouveau jeton de session unique
                var newSessionToken = Guid.NewGuid().ToString();

                var superAdminSetting = await _context.AppSettings.FindAsync("SuperAdminUserId");
                string roleName = ((UserRole)user.Statut).ToString();
                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.ID_User),
            new Claim(ClaimTypes.Name, user.User_name),
            new Claim(ClaimTypes.Role, roleName),
            new Claim("CodeDIW", user.CodeDIW ?? ""),
            // 2. Ajouter le nouveau jeton au cookie de l'utilisateur
            new Claim("SessionToken", newSessionToken)
        };
                // --- FIN DE LA NOUVELLE LOGIQUE DE SESSION ---

                if (superAdminSetting != null && user.ID_User == superAdminSetting.Value)
                {
                    claims.Add(new Claim("IsSuperAdmin", "true"));
                }
                else if (user.Statut == (int)UserRole.Admin)
                {
                    foreach (var permission in user.UserPermissions)
                    {
                        claims.Add(new Claim("Permission", permission.PermissionId));
                    }
                }

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties { IsPersistent = model.KeepLoggedIn };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

                // --- NOUVELLE LOGIQUE DE SAUVEGARDE ---
                // 3. Mettre à jour l'utilisateur dans la base de données avec le nouveau jeton
                user.LastCnx = DateTime.Now;
                user.SessionToken = newSessionToken; // Sauvegarder le jeton
                _context.Update(user);
                await _context.SaveChangesAsync();
                // --- FIN DE LA NOUVELLE LOGIQUE DE SAUVEGARDE ---

                if (superAdminSetting != null && user.ID_User == superAdminSetting.Value)
                {
                    return RedirectToAction("Index", "SuperAdmin");
                }

                return (UserRole)user.Statut switch
                {
                    UserRole.DIW => RedirectToAction("Index", "DIW"),
                    UserRole.DRI => RedirectToAction("Index", "DRI"),
                    UserRole.DC => RedirectToAction("Index", "DC"),
                    UserRole.Admin => RedirectToAction("Index", "Admin"),
                    _ => RedirectToAction("Login"),
                };
            }

            ViewData["ValidateMessage"] = "Utilisateur ou mot de passe incorrect.";
            return View(model);
        }

        // The rest of your AccessController methods (LogOut, ChangePassword, etc.) remain the same.
        // [Fichier: AccessController.cs]
        [HttpGet] // <-- MODIFIÉ
        public async Task<IActionResult> LogOut()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Access");
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound();
            }
            if (user.Password != sha256_hash(model.OldPassword))
            {
                ModelState.AddModelError("OldPassword", "Incorrect current password.");
                return View(model);
            }
            user.Password = sha256_hash(model.NewPassword);
            user.motdepasse = model.NewPassword;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Password changed successfully.";
            return View();
        }

        // --- DÉBUT DES AJOUTS DANS AccessController.cs ---

        // 1. Afficher la page pour entrer l'email
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // 2. Vérifier si l'email existe
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmail(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                ViewData["Error"] = "Veuillez entrer une adresse email.";
                return View("ForgotPassword");
            }

            // Recherche de l'utilisateur par Email
            var user = await _context.Users.FirstOrDefaultAsync(u => u.MailUser == email);

            if (user == null)
            {
                ViewData["Error"] = "L'adresse email saisie ne correspond à aucun compte utilisateur.";
                return View("ForgotPassword");
            }

            // Si l'utilisateur existe, on le redirige vers la vue de changement de mot de passe
            // On passe l'ID de l'utilisateur au modèle
            var model = new ResetPasswordViewModel { UserId = user.ID_User };
            return View("ResetPasswordFromRecovery", model);
        }

        // 3. Enregistrer le nouveau mot de passe
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPasswordConfirm(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("ResetPasswordFromRecovery", model);
            }

            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            // Mise à jour des mots de passe (Hashé + Clair comme dans votre structure)
            user.Password = sha256_hash(model.NewPassword);
            user.motdepasse = model.NewPassword; // Mise à jour du champ en clair

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // Message de succès stocké dans TempData pour l'afficher sur la page de Login
            TempData["SuccessMessage"] = "Votre mot de passe a été réinitialisé avec succès. Vous pouvez maintenant vous connecter.";

            return RedirectToAction("Login");
        }
        // --- FIN DES AJOUTS ---
    }
}