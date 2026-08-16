using Firetrack.Views;
using Microsoft.Maui.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Firetrack;

public partial class AppShell : Shell, INotifyPropertyChanged
{
    private bool _isAdmin;
    private bool _isPersonnel;

    public bool IsAdmin
    {
        get => _isAdmin;
        set { _isAdmin = value; OnPropertyChanged(); }
    }

    public bool IsPersonnel
    {
        get => _isPersonnel;
        set { _isPersonnel = value; OnPropertyChanged(); }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    protected new void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public AppShell()
    {
        InitializeComponent();
        BindingContext = this;

        // ✅ Register ALL pages
        Routing.RegisterRoute("AddEquipmentPage", typeof(AddEquipmentPage));
        Routing.RegisterRoute("UserManagementPage", typeof(UserManagementPage));
        Routing.RegisterRoute("NotificationsPage", typeof(NotificationsPage));
        Routing.RegisterRoute("TransactionHistoryPage", typeof(TransactionHistoryPage));
        Routing.RegisterRoute("IcsPage", typeof(IcsPage));
        Routing.RegisterRoute("ReportDamagePage", typeof(ReportDamagePage));
        Routing.RegisterRoute("AuditLogPage", typeof(AuditLogPage));
        Routing.RegisterRoute("EquipmentDetailPage", typeof(EquipmentDetailPage));
        Routing.RegisterRoute("EquipmentRequestDetailPage", typeof(EquipmentRequestDetailPage));
        Routing.RegisterRoute("CategoryItemsPage", typeof(CategoryItemsPage));
        Routing.RegisterRoute("EquipmentCategoryPage", typeof(EquipmentCategoryPage));
        Routing.RegisterRoute("PendingRequestsPage", typeof(PendingRequestsPage));
        Routing.RegisterRoute("TransferPage", typeof(TransferPage));
        Routing.RegisterRoute("ClearancePage", typeof(ClearancePage));
        Routing.RegisterRoute("ProfilePage", typeof(ProfilePage));
        Routing.RegisterRoute("DashboardPage", typeof(DashboardPage));
        Routing.RegisterRoute("LoginPage", typeof(LoginPage));
        Routing.RegisterRoute("ForgotPasswordPage", typeof(ForgotPasswordPage));
        Routing.RegisterRoute("ScannerPage", typeof(ScannerPage));
        Routing.RegisterRoute("GenerateQRPage", typeof(GenerateQRPage));

        UpdateUserRoleVisibility();
    }

    public void UpdateUserRoleVisibility()
    {
        var user = App.CurrentUser;
        if (user == null)
        {
            IsAdmin = false;
            IsPersonnel = false;
        }
        else if (user.Role == "Admin")
        {
            IsAdmin = true;
            IsPersonnel = false;
        }
        else // Personnel
        {
            IsAdmin = false;
            IsPersonnel = true;
        }

        OnPropertyChanged(nameof(IsAdmin));
        OnPropertyChanged(nameof(IsPersonnel));
    }

    // ---- Route Validation (Anomaly Detection) ----
    private readonly HashSet<string> _validRoutes = new()
    {
        "LoginPage", "ForgotPasswordPage", "DashboardPage",
        "ScannerPage", "GenerateQRPage", "TransferPage",
        "ClearancePage", "ProfilePage", "NotificationsPage",
        "EquipmentCategoryPage", "CategoryItemsPage",
        "EquipmentDetailPage", "EquipmentRequestDetailPage",
        "ReportDamagePage", "IcsPage", "TransactionHistoryPage",
        "UserManagementPage",
        "AddEquipmentPage",
        "PendingRequestsPage", "AuditLogPage"
    };

    private bool IsValidRoute(string route)
    {
        // Allow relative navigation
        if (route == ".." || string.IsNullOrEmpty(route))
            return true;

        return _validRoutes.Contains(route);
    }

    // ---- Permission Check (Role-Based) ----
    private bool IsRouteAllowed(string route)
    {
        var user = App.CurrentUser;
        bool isLoggedIn = user != null;
        bool isAdmin = isLoggedIn && user!.Role == "Admin";

        if (!isLoggedIn)
            return route == "LoginPage" || route == "ForgotPasswordPage";

        if (route == "..")
            return true;

        if (route == "LoginPage" || route == "ForgotPasswordPage")
            return false;

        if (isAdmin)
            return true;

        // Personnel-allowed routes
        var allowedForPersonnel = new HashSet<string>
        {
            "DashboardPage",
            "ProfilePage",
            "RequestEquipmentPage",
            "ReportDamagePage",
            "ScannerPage",
            "NotificationsPage",
            "TransactionHistoryPage",
            "IcsPage",
            "EquipmentRequestDetailPage",
            "EquipmentCategoryPage",
            "CategoryItemsPage",
            "GenerateQRPage"
        };

        return allowedForPersonnel.Contains(route);
    }

    // ---- Navigation Guard with Anomaly Detection ----
    protected override void OnNavigating(ShellNavigatingEventArgs args)
    {
        base.OnNavigating(args);

        var target = args.Target.Location.OriginalString;
        var route = target.Split('/').Last();
        var user = App.CurrentUser;

        // ✅ LOG EVERY NAVIGATION ATTEMPT
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        System.Diagnostics.Debug.WriteLine($"[NAV] [{timestamp}] User: {user?.Username ?? "Anonymous"} | Role: {user?.Role ?? "None"} | Target: {target} | Route: {route}");

        // ✅ ANOMALY CHECK 1: Invalid route (not in valid list)
        if (!IsValidRoute(route))
        {
            System.Diagnostics.Debug.WriteLine($"[NAV ⚠️ ANOMALY] Invalid route detected: {route} from user {user?.Username ?? "Anonymous"}");
            args.Cancel();
            Application.Current?.MainPage?.DisplayAlert(
                "Navigation Error",
                $"The page '{route}' does not exist or is not accessible.",
                "OK");
            return;
        }

        // ✅ ANOMALY CHECK 2: Permission violation (role-based)
        if (!IsRouteAllowed(route))
        {
            System.Diagnostics.Debug.WriteLine($"[NAV ⚠️ ANOMALY] Permission denied: {route} for user {user?.Username ?? "Anonymous"} (Role: {user?.Role ?? "None"})");
            args.Cancel();

            if (user == null)
            {
                GoToAsync("//LoginPage");
            }
            else
            {
                GoToAsync("//DashboardPage");
                Application.Current?.MainPage?.DisplayAlert(
                    "Access Denied",
                    "You do not have permission to view this page.",
                    "OK");
            }
            return;
        }

        // ✅ All checks passed
        System.Diagnostics.Debug.WriteLine($"[NAV ✅] Allowed: {route}");
    }
}