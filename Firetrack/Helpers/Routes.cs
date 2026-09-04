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

        // ---- Admin-only pages (only one occurrence, so no ambiguity) ----
        public const string Transfer = "//TransferPage";
        public const string Clearance = "//ClearancePage";
        public const string UserManagement = "//UserManagementPage";
        public const string PendingRequests = "//PendingRequestsPage";
        public const string DisposalRequests = "//DisposalRequestsPage";
        public const string AuditLog = "//AuditLogPage";
        public const string AddEquipment = "//AddEquipmentPage";

        // ---- Shared pages (used by both roles) ----
        public const string Profile = "//ProfilePage";
        public const string Notifications = "//MyNotifications";
        public const string EquipmentDetail = "//EquipmentDetailPage";
        public const string EquipmentRequestDetail = "//EquipmentRequestDetailPage";
        public const string ReportDamage = "//ReportDamagePage";
        public const string Ics = "//IcsPage";
        public const string CategoryItems = "//CategoryItemsPage";
        public const string TransactionHistory = "//TransactionHistoryPage";

        // ---- Role-aware routes (must call methods) ----
        public static string GetEquipmentCategoryRoute()
        {
            var user = App.CurrentUser;
            // Routes are now unique: "AdminEquipmentCategory" and "PersonnelEquipmentCategory"
            return user?.Role == "Admin" ? "//AdminEquipmentCategory" : "//PersonnelEquipmentCategory";
        }

        public static string GetScannerRoute()
        {
            var user = App.CurrentUser;
            // Routes are already unique: "AdminScanner" and "PersonnelScanner"
            return user?.Role == "Admin" ? "//AdminScanner" : "//PersonnelScanner";
        }

        public static string GetDashboardRoute()
        {
            var user = App.CurrentUser;
            return user?.Role == "Admin" ? AdminDashboard : PersonnelDashboard;
        }
    }
}