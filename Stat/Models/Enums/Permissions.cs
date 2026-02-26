namespace Stat.Models.Enums
{
    public static class Permissions
    {
        // Admin Dashboard
        public const string ViewAdminDashboard = "Permissions.AdminDashboard.View";

        // User Management
        public const string ViewUsers = "Permissions.Users.View";
        public const string ManageUsers = "Permissions.Users.Manage";

        // Structure Management
        public const string ManageDRIs = "Permissions.Structures.ManageDRIs";
        public const string ManageDIWs = "Permissions.Structures.ManageDIWs";

        public const string ManageDCs = "Permissions.Structures.ManageDCs";

        // Target Management
        public const string ManageStrategicTargets = "Permissions.Targets.ManageStrategic";
        public const string ManageOperationalTargets = "Permissions.Targets.ManageOperational";

        // Indicator Management
        public const string ManageStrategicIndicators = "Permissions.Indicators.ManageStrategic";
        public const string ManageOperationalIndicators = "Permissions.Indicators.ManageOperational";

        // Analysis Dashboards
        public const string ViewStrategicAnalysis = "Permissions.Analysis.ViewStrategic";
        public const string ViewOperationalAnalysis = "Permissions.Analysis.ViewOperational";

        // Settings
        public const string ManageAxes = "Permissions.Settings.ManageAxes";
        public const string ManageObjectives = "Permissions.Settings.ManageObjectives";

        public const string ViewValidatedReports = "Permissions.Reports.ViewValidated";
        public const string ValidateSituations = "Permissions.Situations.Validate";
        
        public const string ManageRapports = "Permissions.Rapports.Manage";



    }
}