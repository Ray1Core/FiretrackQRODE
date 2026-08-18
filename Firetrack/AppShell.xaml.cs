using Firetrack.Views;
using Microsoft.Maui.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Firetrack;

public partial class AppShell : Shell, INotifyPropertyChanged
{
    private bool _isAdmin;
    private bool _isPersonnel;
    private int _unreadCount;

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

    public int UnreadCount
    {
        get => _unreadCount;
        set
        {
            _unreadCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NotificationBadgeText));
        }
    }

    public string NotificationBadgeText
    {
        get => UnreadCount > 0 ? $"🔔 {UnreadCount}" : "🔔";
    }

    // Logout command for flyout footer
    public ICommand LogoutCommand { get; }
    public ICommand GoToNotificationsCommand { get; }

    public new event PropertyChangedEventHandler? PropertyChanged;

    protected new void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public AppShell()
    {
        InitializeComponent();
        BindingContext = this;

        LogoutCommand = new Command(OnLogout);
        GoToNotificationsCommand = new Command(async () => await GoToAsync("//NotificationsPage"));

        UpdateUserRoleVisibility();
        LoadUnreadCount();
    }

    // ✅ Make this method public so it can be called from ViewModels
    public async void LoadUnreadCount()
    {
        if (App.CurrentUser == null || App.Database == null) return;
        try
        {
            var notifications = await App.Database.GetNotificationsForUserAsync(App.CurrentUser.Username);
            UnreadCount = notifications.Count(n => !n.IsRead);
        }
        catch
        {
            UnreadCount = 0;
        }
    }

    // ✅ Static method to refresh the badge from anywhere
    public static void RefreshUnreadCount()
    {
        var shell = Current as AppShell;
        shell?.LoadUnreadCount();
    }

    private async void OnLogout()
    {
        if (App.CurrentUser != null && App.Database != null)
        {
            await App.Database.LogActionAsync(
                App.CurrentUser.Username,
                "Logout",
                "User logged out");
        }

        App.CurrentUser = null;
        UpdateUserRoleVisibility();
        await GoToAsync("//LoginPage");
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
        else
        {
            IsAdmin = false;
            IsPersonnel = true;
        }

        OnPropertyChanged(nameof(IsAdmin));
        OnPropertyChanged(nameof(IsPersonnel));
    }

    // ---- Route Validation ----
    private readonly HashSet<string> _validRoutes = new()
    {
        "LoginPage", "ForgotPasswordPage", "DashboardPage",
        "ScannerPage", "TransferPage",
        "ClearancePage", "ProfilePage", "NotificationsPage",
        "EquipmentCategoryPage", "CategoryItemsPage",
        "EquipmentDetailPage", "EquipmentRequestDetailPage",
        "ReportDamagePage", "IcsPage", "TransactionHistoryPage",
        "UserManagementPage",
        "AddEquipmentPage",
        "PendingRequestsPage",
        "AuditLogPage",
        "DisposalRequestsPage"
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

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        System.Diagnostics.Debug.WriteLine($"[NAV] [{timestamp}] User: {user?.Username ?? "Anonymous"} | Role: {user?.Role ?? "None"} | Target: {target} | Route: {route}");

        if (!IsValidRoute(route))
        {
            System.Diagnostics.Debug.WriteLine($"[NAV ⚠️ ANOMALY] Invalid route: {route}");
            args.Cancel();
            Application.Current?.MainPage?.DisplayAlert(
                "Navigation Error",
                $"The page '{route}' does not exist or is not accessible.",
                "OK");
            return;
        }

        if (!IsRouteAllowed(route))
        {
            System.Diagnostics.Debug.WriteLine($"[NAV ⚠️ ANOMALY] Permission denied: {route}");
            args.Cancel();
            if (user == null)
                GoToAsync("//LoginPage");
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

        System.Diagnostics.Debug.WriteLine($"[NAV ✅] Allowed: {route}");
    }
}