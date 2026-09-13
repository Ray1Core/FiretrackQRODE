using Firetrack.Views;
using Firetrack.Helpers;
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

    private string GetDashboardRoute()
    {
        var user = App.CurrentUser;
        return user?.Role == "Admin" ? Routes.AdminDashboard : Routes.PersonnelDashboard;
    }

    public AppShell()
    {
        try
        {
            InitializeComponent();

            // ===== CRITICAL FIX: Register detail pages as global routes =====
            // This allows them to be pushed onto the navigation stack (shows back button)
            // and prevents the "Relative routing to shell elements" crash.
            Routing.RegisterRoute(nameof(CategoryItemsPage), typeof(CategoryItemsPage));
            Routing.RegisterRoute(nameof(EquipmentDetailPage), typeof(EquipmentDetailPage));
            Routing.RegisterRoute(nameof(EquipmentRequestDetailPage), typeof(EquipmentRequestDetailPage));
            Routing.RegisterRoute(nameof(ReportDamagePage), typeof(ReportDamagePage));
            Routing.RegisterRoute(nameof(IcsPage), typeof(IcsPage));
            Routing.RegisterRoute(nameof(TransactionHistoryPage), typeof(TransactionHistoryPage));
            Routing.RegisterRoute(nameof(AddEquipmentPage), typeof(AddEquipmentPage));
            Routing.RegisterRoute(nameof(NotificationsPage), typeof(NotificationsPage));

            // Set BindingContext so FlyoutItem IsVisible bindings work
            BindingContext = this;
            TitleViewGrid.BindingContext = this;

            UpdateUserRoleVisibility();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ AppShell constructor error: {ex}");
            throw;
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        if (App.CurrentUser != null && App.Database != null)
        {
            await App.Database.LogActionAsync(
                App.CurrentUser.Email,
                "Logout",
                "User logged out");
        }

        App.CurrentUser = null;
        UpdateUserRoleVisibility();
        await GoToAsync(Routes.Login);
    }

    // ===== ROLE VISIBILITY =====
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
        else
        {
            IsAdmin = false;
            IsPersonnel = true;
        }

        // Force UI update
        OnPropertyChanged(nameof(IsAdmin));
        OnPropertyChanged(nameof(IsPersonnel));

        System.Diagnostics.Debug.WriteLine($"🔍 UpdateUserRoleVisibility: IsAdmin={IsAdmin}, IsPersonnel={IsPersonnel}, User={user?.Email}");
    }

    // ===== ROUTE VALIDATION =====
    private readonly HashSet<string> _validRoutes = new()
    {
        // Root pages (absolute routes)
        "LoginPage", "ForgotPasswordPage",
        "AdminDashboard", "PersonnelDashboard",
        "AdminEquipmentCategory", "PersonnelEquipmentCategory",
        "TransferPage", "ClearancePage", "UserManagementPage",
        "PendingRequestsPage", "DisposalRequestsPage",
        "AuditLogPage", "ProfilePage",
        "AdminScanner", "PersonnelScanner",

        // Detail pages (relative routes, pushed onto stack)
        "CategoryItemsPage",
        "EquipmentDetailPage",
        "EquipmentRequestDetailPage",
        "ReportDamagePage",
        "IcsPage",
        "TransactionHistoryPage",
        "AddEquipmentPage",
        "NotificationsPage"
    };

    private bool IsValidRoute(string route)
    {
        if (route == ".." || string.IsNullOrEmpty(route))
            return true;
        return _validRoutes.Contains(route);
    }

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

        // Personnel allowed routes
        var allowedForPersonnel = new HashSet<string>
        {
            "PersonnelDashboard",
            "ProfilePage",
            "ReportDamagePage",
            "PersonnelScanner",
            "NotificationsPage",
            "TransactionHistoryPage",
            "IcsPage",
            "EquipmentRequestDetailPage",
            "PersonnelEquipmentCategory",
            "CategoryItemsPage"
        };

        return allowedForPersonnel.Contains(route);
    }

    protected override void OnNavigating(ShellNavigatingEventArgs args)
    {
        base.OnNavigating(args);

        var target = args.Target.Location.OriginalString;
        var route = target.Split('/').Last();
        var user = App.CurrentUser;

        if (!IsValidRoute(route))
        {
            args.Cancel();
            Application.Current?.MainPage?.DisplayAlert(
                "Navigation Error",
                $"The page '{route}' does not exist or is not accessible.",
                "OK");
            return;
        }

        if (!IsRouteAllowed(route))
        {
            args.Cancel();
            if (user == null)
                GoToAsync(Routes.Login);
            else
            {
                GoToAsync(GetDashboardRoute());
                Application.Current?.MainPage?.DisplayAlert(
                    "Access Denied",
                    "You do not have permission to view this page.",
                    "OK");
            }
            return;
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateUserRoleVisibility();
    }
}