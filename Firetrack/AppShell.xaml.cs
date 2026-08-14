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

        Routing.RegisterRoute("AddEquipmentPage", typeof(AddEquipmentPage));
        Routing.RegisterRoute("NotificationsPage", typeof(NotificationsPage));
        Routing.RegisterRoute("TransactionHistoryPage", typeof(TransactionHistoryPage));
        Routing.RegisterRoute("IcsPage", typeof(IcsPage));
        Routing.RegisterRoute("ReportDamagePage", typeof(ReportDamagePage));
        Routing.RegisterRoute("AuditLogPage", typeof(AuditLogPage));
        Routing.RegisterRoute("EquipmentDetailPage", typeof(EquipmentDetailPage));
        Routing.RegisterRoute("EquipmentRequestDetailPage", typeof(EquipmentRequestDetailPage));
        Routing.RegisterRoute("CategoryItemsPage", typeof(CategoryItemsPage));    
        Routing.RegisterRoute("InventoryCategoryGridPage", typeof(InventoryCategoryGridPage));

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

    private bool IsRouteAllowed(string route)
    {
        var user = App.CurrentUser;
        bool isLoggedIn = user != null;
        bool isAdmin = isLoggedIn && user!.Role == "Admin";

        if (!isLoggedIn)
            return route == "LoginPage" || route == "ForgotPasswordPage";

        // Allow back navigation
        if (route == "..")
            return true;

        if (route == "LoginPage" || route == "ForgotPasswordPage")
            return false;

        if (isAdmin)
            return true;

        var allowedForPersonnel = new[]
        {
        "DashboardPage",
        "ProfilePage",
        "RequestEquipmentPage",
        "ReportDamagePage",
        "ScannerPage",
        "NotificationsPage",
        "TransactionHistoryPage",
        "IcsPage",
        "EquipmentRequestDetailPage"
    };

        return allowedForPersonnel.Contains(route);
    }

    protected override void OnNavigating(ShellNavigatingEventArgs args)
    {
        base.OnNavigating(args);

        var target = args.Target.Location.OriginalString;
        var route = target.Split('/').Last();

        System.Diagnostics.Debug.WriteLine($"🛡️ Navigation Guard: Route={route}, User={App.CurrentUser?.Username}, Role={App.CurrentUser?.Role}");

        if (!IsRouteAllowed(route))
        {
            System.Diagnostics.Debug.WriteLine($"🚫 Blocked: {route}");
            args.Cancel();

            if (App.CurrentUser == null)
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
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"✅ Allowed: {route}");
        }
    }
}