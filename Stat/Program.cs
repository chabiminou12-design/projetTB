using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Stat.Models;
using QuestPDF.Infrastructure;
using Stat.Models.Enums;
using Stat.Services;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using Microsoft.AspNetCore.DataProtection;
using System.IO;

var builder = WebApplication.CreateBuilder(args);
QuestPDF.Settings.License = LicenseType.Community;

// --- Services Configuration ---

builder.Services.AddControllersWithViews(options =>
{
    // This stops the framework from automatically making properties required.
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<UserInfoService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Database Configuration
builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("dbConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure()
    ).EnableSensitiveDataLogging()
);

// Authentication Configuration
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => {
        options.LoginPath = "/Access/Login";
        options.AccessDeniedPath = "/Access/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
    });

builder.Services.AddScoped<Stat.Services.IReportService, Stat.Services.ReportService>();

// Authorization Configuration
builder.Services.AddAuthorization(options =>
{
    // ... (Your Authorization Policies)
    options.AddPolicy("DIWAccess", policy => policy.RequireRole("DIW"));
    options.AddPolicy("DRIAccess", policy => policy.RequireRole("DRI"));
    options.AddPolicy("DCAccess", policy => policy.RequireRole("DC"));
    options.AddPolicy("AdminAccess", policy => policy.RequireRole("Admin"));
    options.AddPolicy("DirectorAccess", policy => policy.RequireRole("Director"));
    options.AddPolicy("SuperAdminOnly", policy => policy.RequireClaim("IsSuperAdmin", "true"));

    // Permission-based policies
    options.AddPolicy(Permissions.ViewAdminDashboard, policy => policy.RequireClaim("Permission", Permissions.ViewAdminDashboard));
    options.AddPolicy(Permissions.ViewUsers, policy => policy.RequireClaim("Permission", Permissions.ViewUsers));
    options.AddPolicy(Permissions.ManageUsers, policy => policy.RequireClaim("Permission", Permissions.ManageUsers));
    options.AddPolicy(Permissions.ManageDRIs, policy => policy.RequireClaim("Permission", Permissions.ManageDRIs));
    options.AddPolicy(Permissions.ManageDIWs, policy => policy.RequireClaim("Permission", Permissions.ManageDIWs));
    options.AddPolicy(Permissions.ManageStrategicTargets, policy => policy.RequireClaim("Permission", Permissions.ManageStrategicTargets));
    options.AddPolicy(Permissions.ManageOperationalTargets, policy => policy.RequireClaim("Permission", Permissions.ManageOperationalTargets));
    options.AddPolicy(Permissions.ManageStrategicIndicators, policy => policy.RequireClaim("Permission", Permissions.ManageStrategicIndicators));
    options.AddPolicy(Permissions.ManageOperationalIndicators, policy => policy.RequireClaim("Permission", Permissions.ManageOperationalIndicators));
    options.AddPolicy(Permissions.ViewStrategicAnalysis, policy => policy.RequireClaim("Permission", Permissions.ViewStrategicAnalysis));
    options.AddPolicy(Permissions.ViewOperationalAnalysis, policy => policy.RequireClaim("Permission", Permissions.ViewOperationalAnalysis));
    options.AddPolicy(Permissions.ManageAxes, policy => policy.RequireClaim("Permission", Permissions.ManageAxes));
    options.AddPolicy(Permissions.ManageObjectives, policy => policy.RequireClaim("Permission", Permissions.ManageObjectives));
    options.AddPolicy(Permissions.ViewValidatedReports, policy => policy.RequireClaim("Permission", Permissions.ViewValidatedReports));
    options.AddPolicy(Permissions.ManageDCs, policy => policy.RequireClaim("Permission", Permissions.ManageDCs));
    options.AddPolicy(Permissions.ValidateSituations, policy => policy.RequireClaim("Permission", Permissions.ValidateSituations));
    options.AddPolicy(Permissions.ManageRapports, policy => policy.RequireClaim("Permission", Permissions.ManageRapports));
});

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(@"C:\AppKeys\TDBApp\"))
    .SetApplicationName("TDBApp");

var app = builder.Build();

// --- Middleware Pipeline ---

// 2. CRITICAL FIX: Forwarded Headers Middleware
// Modification pour explicitement faire confiance à l'adresse IP du proxy NGINX (10.216.6.9)
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
// ✨ AJOUTER CE BLOCK : Indique à l'application de faire confiance à l'IP du proxy
forwardedHeadersOptions.KnownProxies.Add(IPAddress.Parse("10.216.6.9"));

app.UseForwardedHeaders(forwardedHeadersOptions);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios.
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<SessionValidatorMiddleware>();

app.UseEndpoints(endpoints => {
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Access}/{action=Login}/{id?}");
});

app.Run();