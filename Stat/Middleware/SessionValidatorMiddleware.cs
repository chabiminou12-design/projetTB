// [Fichier: Middleware/SessionValidatorMiddleware.cs]
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Stat.Models;
using System.Security.Claims;

public class SessionValidatorMiddleware
{
    private readonly RequestDelegate _next;

    public SessionValidatorMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, DatabaseContext dbContext)
    {
        // On ne procède que si l'utilisateur est authentifié
        if (context.User.Identity?.IsAuthenticated ?? false)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var sessionTokenFromCookie = context.User.FindFirstValue("SessionToken");

            // Il faut absolument un ID utilisateur et un jeton de session pour continuer
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(sessionTokenFromCookie))
            {
                await InvalidateSession(context);
                return;
            }

            var user = await dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.ID_User == userId);

            // Si l'utilisateur a été supprimé ou que le jeton ne correspond pas, la session est invalide
            if (user == null || user.SessionToken != sessionTokenFromCookie)
            {
                await InvalidateSession(context);
                return;
            }
        }

        await _next(context); // Si tout est valide, continuer vers la page demandée
    }

    private static async Task InvalidateSession(HttpContext context)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        context.Response.Redirect("/Access/Login");
    }
}