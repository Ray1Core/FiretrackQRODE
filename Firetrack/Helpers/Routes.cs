using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Firetrack.Helpers
{
    public static class Routes
    {
        // ---- Absolute routes (must start with "//") ----
        public const string Login = "//LoginPage";
        public const string ForgotPassword = "//ForgotPasswordPage";

        // Role-specific dashboards
        public const string AdminDashboard = "//AdminDashboard";
        public const string PersonnelDashboard = "//PersonnelDashboard";

        // Other pages (note: Scanner is now role-aware, so we keep the old constant for reference)
        public const string EquipmentCategory = "//EquipmentCategoryPage";
        public const string Transfer = "//TransferPage";
        public const string Clearance = "//ClearancePage";
        public const string UserManagement = "//UserManagementPage";
        public const string PendingRequests = "//PendingRequestsPage";
        public const string DisposalRequests = "//DisposalRequestsPage";
        public const string AuditLog = "//AuditLogPage";
        public const string Profile = "//ProfilePage";

        // Notifications – unique route
        public const string Notifications = "//MyNotifications";

        public const string EquipmentDetail = "//EquipmentDetailPage";
        public const string EquipmentRequestDetail = "//EquipmentRequestDetailPage";
        public const string ReportDamage = "//ReportDamagePage";
        public const string Ics = "//IcsPage";
        public const string CategoryItems = "//CategoryItemsPage";
        public const string TransactionHistory = "//TransactionHistoryPage";
        public const string AddEquipment = "//AddEquipmentPage";

        // ---- Role‑aware scanner route ----
        public static string GetScannerRoute()
        {
            var user = App.CurrentUser;
            return user?.Role == "Admin" ? "//AdminScanner" : "//PersonnelScanner";
        }

        // ---- Helper to get the correct dashboard for the current user ----
        public static string GetDashboardRoute()
        {
            var user = App.CurrentUser;
            return user?.Role == "Admin" ? AdminDashboard : PersonnelDashboard;
        }
    }
}