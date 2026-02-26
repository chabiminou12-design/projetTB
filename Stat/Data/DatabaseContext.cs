using Microsoft.EntityFrameworkCore;
using Stat.Models;
using System;

namespace Stat.Models
{
    public partial class DatabaseContext : DbContext
    {
        public DatabaseContext()
        {
        }

        public DatabaseContext(DbContextOptions<DatabaseContext> options)
            : base(options)
        {
        }

        public virtual DbSet<User>? Users { get; set; }
        public virtual DbSet<CategorieIndicateur>? CategorieIndicateurs { get; set; }
        public virtual DbSet<Indicateur>? Indicateurs { get; set; }
        public virtual DbSet<Situation>? Situations { get; set; }

        public virtual DbSet<DC>? DCs { get; set; }
        public DbSet<DRI> DRIs { get; set; }
        public DbSet<DIW> DIWs { get; set; }
        public DbSet<Cible> cibles { get; set; }
        public DbSet<Declaration> Declarations { get; set; }

        public DbSet<DeclarationDraft> DeclarationDrafts { get; set; }
        public DbSet<RejectionHistory> RejectionHistories { get; set; }
        public DbSet<IndicateurStrategique> IndicateursStrategiques { get; set; }
       
        public DbSet<DeclarationStrategique> DeclarationsStrategiques { get; set; }
        public DbSet<DeclarationStrategiqueDraft> DeclarationsStrategiquesDrafts { get; set; }
        public DbSet<Objectif> Objectifs { get; set; }
        public DbSet<cible_stratigique> ciblesStrategiques { get; set; }
        public DbSet<Indicateurs_DE_PERFORMANCE_OPERATIONNELS> Indicateurs_DE_PERFORMANCE_OPERATIONNELS { get; set; }
        public DbSet<cibles_de_performance_dri> cibles_de_performance_dri { get; set; }
        public DbSet<DeclarationDRI> DeclarationDRIs { get; set; }
        public DbSet<DeclarationDRIDraft> DeclarationDRIDrafts { get; set; }
        public DbSet<Rapport> Rapports { get; set; }
        


        // ✨ ADD THESE THREE NEW DbSets
        public DbSet<AppSetting> AppSettings { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // ✅ CONFIGURATION AUTOMATIQUE DES TRIGGERS
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                // On récupère le nom de la table
                var tableName = entityType.GetTableName();

                // On vérifie que c'est une entité liée à une table réelle (pas une vue ou shadow entity)
                if (tableName != null && entityType.ClrType != null)
                {
                    // Utiliser modelBuilder.Entity(entityType.ClrType) pour accéder au Builder
                    modelBuilder.Entity(entityType.ClrType).ToTable(tb => tb.HasTrigger($"trg_Reseed_{tableName}"));
                }
            }

            // --- Define the relationship between Users and Permissions ---
            modelBuilder.Entity<UserPermission>()
                .HasOne(up => up.User)
                .WithMany(u => u.UserPermissions)
                .HasForeignKey(up => up.UserId);

            modelBuilder.Entity<UserPermission>()
                .HasOne(up => up.Permission)
                .WithMany()
                .HasForeignKey(up => up.PermissionId);

            // --- Seed the Permissions table with all available permissions based on your new menu ---
            modelBuilder.Entity<Permission>().HasData(
                // Admin Dashboard
                new Permission { PermissionId = "Permissions.AdminDashboard.View", Description = "Voir le tableau de bord principal de l'administrateur" },

                // User Management
                new Permission { PermissionId = "Permissions.Users.View", Description = "Voir la liste des utilisateurs" },
                new Permission { PermissionId = "Permissions.Users.Manage", Description = "Créer, modifier et supprimer des utilisateurs" },

                // Structure Management
                new Permission { PermissionId = "Permissions.Structures.ManageDRIs", Description = "Gérer les DRIs (créer, supprimer, lister)" },
                new Permission { PermissionId = "Permissions.Structures.ManageDIWs", Description = "Gérer les DIWs (créer, supprimer, lister)" },
                new Permission { PermissionId = "Permissions.Structures.ManageDCs", Description = "Gérer les DCs (créer, supprimer, lister)" },

                // Target Management
                new Permission { PermissionId = "Permissions.Targets.ManageStrategic", Description = "Gérer les cibles stratégiques" },
                new Permission { PermissionId = "Permissions.Targets.ManageOperational", Description = "Gérer les cibles opérationnelles" },

                // Indicator Management
                new Permission { PermissionId = "Permissions.Indicators.ManageStrategic", Description = "Gérer les indicateurs stratégiques" },
                new Permission { PermissionId = "Permissions.Indicators.ManageOperational", Description = "Gérer les indicateurs opérationnels" },

                // Analysis Dashboards
                new Permission { PermissionId = "Permissions.Analysis.ViewStrategic", Description = "Consulter le tableau de bord de niveau stratégique" },
                new Permission { PermissionId = "Permissions.Analysis.ViewOperational", Description = "Consulter le tableau de bord de niveau opérationnel" },

                // Settings
                new Permission { PermissionId = "Permissions.Settings.ManageAxes", Description = "Gérer les Axes Stratégiques" },
                new Permission { PermissionId = "Permissions.Settings.ManageObjectives", Description = "Gérer les Objectifs Stratégiques" },

                // Reports
                new Permission { PermissionId = "Permissions.Reports.ViewValidated", Description = "Consulter les situations validées (tous types)" },

                // Situations
                new Permission { PermissionId = "Permissions.Situations.Validate", Description = "Valider les situations" },

                // Rapports
                new Permission { PermissionId = "Permissions.Rapports.Manage", Description = "Gérer les rapports (création, modification, suppression)" }
            );

            // ... your other OnModelCreating configurations can go here
        }

    }
}