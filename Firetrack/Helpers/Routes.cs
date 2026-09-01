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
        public const string Dashboard = "//DashboardPage";          // fallback
        public const string EquipmentCategory = "//EquipmentCategoryPage";
        public const string Transfer = "//TransferPage";
        public const string Clearance = "//ClearancePage";
        public const string UserManagement = "//UserManagementPage";
        public const string PendingRequests = "//PendingRequestsPage";
        public const string DisposalRequests = "//DisposalRequestsPage";
        public const string Scanner = "//ScannerPage";
        public const string AuditLog = "//AuditLogPage";
        public const string Profile = "//ProfilePage";
        public const string Notifications = "//NotificationsPage";
        public const string EquipmentDetail = "//EquipmentDetailPage";
        public const string EquipmentRequestDetail = "//EquipmentRequestDetailPage";
        public const string ReportDamage = "//ReportDamagePage";
        public const string Ics = "//IcsPage";
        public const string CategoryItems = "//CategoryItemsPage";
        public const string TransactionHistory = "//TransactionHistoryPage";
        public const string AddEquipment = "//AddEquipmentPage";
    }
}