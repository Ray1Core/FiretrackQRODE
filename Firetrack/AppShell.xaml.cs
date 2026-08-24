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
    private bool _isBackVisible;

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

    // NEW: Visibility for the back button in the TitleView
    public bool IsBackVisible
    {
        get => _isBackVisible;
        set { _isBackVisible = value; OnPropertyChanged(); }
    }

    // NEW: Command for the back button
    public ICommand BackCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand GoToNotificationsCommand { get; }

    public new event PropertyChangedEventHandler? PropertyChanged;

    protected new void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // Define root routes (top-level pages that clear the stack)
    private static readonly HashSet<string> RootRoutes = new()
    {
        "DashboardPage",
        "EquipmentCategoryPage",
        "TransferPage",
        "ClearancePage",
        "UserManagementPage",
        "PendingRequestsPage",
        "DisposalRequestsPage",
        "AuditLogPage",
        "ProfilePage",
        "ScannerPage"
    };

    public AppShell()
    {
        InitializeComponent();
        BindingContext = this;

        BackCommand = new Command(OnBack);
        LogoutCommand = new Command(OnLogout);
        GoToNotificationsCommand = new Command(async () => await GoToAsync("//NotificationsPage"));

        // Subscribe to navigation events
        this.Navigated += OnShellNavigated;

        UpdateUserRoleVisibility();
        LoadUnreadCount();

        // Set initial back button visibility
        UpdateBackButtonVisibility();
    }

    private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        UpdateBackButtonVisibility();
    }

    private void UpdateBackButtonVisibility()
    {
        // Get the current route
        var currentRoute = Current.CurrentState?.Location?.OriginalString?.Split('/').LastOrDefault() ?? string.Empty;

        // Back button is visible if the current route is NOT a root route
        IsBackVisible = !RootRoutes.Contains(currentRoute);
    }

    private async void OnBack()
    {
        // Go back one step in the navigation stack
        await GoToAsync("..");
    }

    public void LoadUnreadCount()
    {
        if (App.CurrentUser == null || App.Database == null) return;
        try
        {
            // Use a background thread to avoid blocking UI
            Task.Run(async () =>
            {
                var notifications = await App.Database.GetNotificationsForUserAsync(App.CurrentUser.Email);
                UnreadCount = notifications.Count(n => !n.IsRead);
            });
        }
        catch
        {
            UnreadCount = 0;
        }
    }

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
                App.CurrentUser.Email,
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
    }
}