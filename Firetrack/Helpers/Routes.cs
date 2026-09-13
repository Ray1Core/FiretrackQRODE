namespace Firetrack.Helpers
{
    public static class Routes
    {
        // ---- Absolute routes (root pages, no back button, flyout accessible) ----
        public const string Login = "//LoginPage";
        public const string ForgotPassword = "//ForgotPasswordPage";

        // Role-specific dashboards
        public const string AdminDashboard = "//AdminDashboard";
        public const string PersonnelDashboard = "//PersonnelDashboard";

        // Admin-only root pages
        public const string Transfer = "//TransferPage";
        public const string Clearance = "//ClearancePage";
        public const string UserManagement = "//UserManagementPage";
        public const string PendingRequests = "//PendingRequestsPage";
        public const string DisposalRequests = "//DisposalRequestsPage";
        public const string AuditLog = "//AuditLogPage";

        // Shared root pages
        public const string Profile = "//ProfilePage";

        // ---- Relative routes (detail pages, PUSH onto stack, shows back button) ----
        public const string AddEquipment = "AddEquipmentPage";
        public const string Notifications = "NotificationsPage";
        public const string EquipmentDetail = "EquipmentDetailPage";
        public const string EquipmentRequestDetail = "EquipmentRequestDetailPage";
        public const string ReportDamage = "ReportDamagePage";
        public const string Ics = "IcsPage";
        public const string CategoryItems = "CategoryItemsPage";
        public const string TransactionHistory = "TransactionHistoryPage";

        // ---- Role-aware routes ----
        public static string GetEquipmentCategoryRoute()
        {
            var user = App.CurrentUser;
            return user?.Role == "Admin" ? "//AdminEquipmentCategory" : "//PersonnelEquipmentCategory";
        }

        public static string GetScannerRoute()
        {
            var user = App.CurrentUser;
            return user?.Role == "Admin" ? "//AdminScanner" : "//PersonnelScanner";
        }

        public static string GetDashboardRoute()
        {
            var user = App.CurrentUser;
            return user?.Role == "Admin" ? AdminDashboard : PersonnelDashboard;
        }
    }
}