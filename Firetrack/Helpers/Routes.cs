namespace Firetrack.Helpers
{
    public static class Routes
    {
        // ============================================================
        // ABSOLUTE ROUTES (root pages — flyout accessible, no back button)
        // ============================================================
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

        // ============================================================
        // RELATIVE ROUTES (detail pages — PUSH onto stack, back button)
        // ============================================================
        public const string AddEquipment = "AddEquipmentPage";
        public const string Notifications = "NotificationsPage";
        public const string EquipmentDetail = "EquipmentDetailPage";
        public const string EquipmentRequestDetail = "EquipmentRequestDetailPage";
        public const string ReportDamage = "ReportDamagePage";
        public const string Ics = "IcsPage";
        public const string CategoryItems = "CategoryItemsPage";
        public const string TransactionHistory = "TransactionHistoryPage";

        // ============================================================
        // PUSHED ROUTES (second route for flyout pages — gives back button
        // when launched from Dashboard quick actions)
        // ============================================================
        // Same page types as the absolute routes above, but registered as
        // global routes in AppShell.xaml.cs. Tapping the flyout uses the
        // absolute version (hamburger); tapping a Dashboard quick action
        // uses the pushed version (back arrow → returns to Dashboard).
        public const string TransferPushed = "TransferPagePushed";
        public const string ClearancePushed = "ClearancePagePushed";
        public const string UserManagementPushed = "UserManagementPagePushed";
        public const string PendingRequestsPushed = "PendingRequestsPagePushed";
        public const string DisposalRequestsPushed = "DisposalRequestsPagePushed";
        public const string AuditLogPushed = "AuditLogPagePushed";
        public const string ProfilePushed = "ProfilePagePushed";
        public const string ScannerPushed = "ScannerPushed";

        // ============================================================
        // ROLE-AWARE ROUTE HELPERS
        // ============================================================

        /// <summary>
        /// Absolute (flyout/root) Equipment Category route — role aware.
        /// </summary>
        public static string GetEquipmentCategoryRoute()
        {
            var user = App.CurrentUser;
            return user?.Role == "Admin"
                ? "//AdminEquipmentCategory"
                : "//PersonnelEquipmentCategory";
        }

        /// <summary>
        /// Absolute (flyout/root) Scanner route — role aware.
        /// </summary>
        public static string GetScannerRoute()
        {
            var user = App.CurrentUser;
            return user?.Role == "Admin"
                ? "//AdminScanner"
                : "//PersonnelScanner";
        }

        /// <summary>
        /// Pushed Scanner route — same ScannerPage type for both roles,
        /// but pushed onto the stack so it shows a back button.
        /// </summary>
        public static string GetScannerPushedRoute()
        {
            // Same route name for both roles; ScannerPage is role-aware
            // through App.CurrentUser and doesn't need two registrations.
            return ScannerPushed;
        }

        /// <summary>
        /// Absolute Dashboard route — role aware.
        /// </summary>
        public static string GetDashboardRoute()
        {
            var user = App.CurrentUser;
            return user?.Role == "Admin"
                ? AdminDashboard
                : PersonnelDashboard;
        }
    }
}