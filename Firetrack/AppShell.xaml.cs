using Firetrack.Views;
using Firetrack.Helpers;
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

    // ---- Properties for flyout visibility ----
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

    // ---- Notification badge ----
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

    // ---- Back button visibility ----
    public bool IsBackVisible
    {
        get => _isBackVisible;
        set { _isBackVisible = value; OnPropertyChanged(); }
    }

    // ---- Commands ----
    public ICommand BackCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand GoToNotificationsCommand { get; }

    public new event PropertyChangedEventHandler? PropertyChanged;

    protected new void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ---- Helper to get the correct dashboard route ----
    private string GetDashboardRoute()
    {
        var user = App.CurrentUser;
        return user?.Role == "Admin" ? Routes.AdminDashboard : Routes.PersonnelDashboard;
    }

    // ---- Root routes for back button visibility (only top‑level pages) ----
    private static readonly HashSet<string> RootRoutes = new()
    {
        "AdminDashboard",
        "PersonnelDashboard",
        "AdminEquipmentCategory",   // ← NEW
        "PersonnelEquipmentCategory", // ← NEW
        "TransferPage",
        "ClearancePage",
        "UserManagementPage",
        "PendingRequestsPage",
        "DisposalRequestsPage",
        "AuditLogPage",
        "ProfilePage",
        "AdminScanner",
        "PersonnelScanner"
    };

    // ---- Constructor ----
    public AppShell()
    {
        try
        {
            InitializeComponent();

            // Bind the TitleView Grid to this Shell for IsBackVisible binding
            TitleViewGrid.BindingContext = this;

            // Commands
            BackCommand = new Command(OnBack);
            LogoutCommand = new Command(OnLogout);
            GoToNotificationsCommand = new Command(async () => await GoToAsync(Routes.Notifications));

            this.Navigated += OnShellNavigated;

            UpdateUserRoleVisibility();
            LoadUnreadCount();
            UpdateBackButtonVisibility();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ AppShell constructor error: {ex}");
            throw;
        }
    }

    // ---- Navigation events ----
    private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        UpdateBackButtonVisibility();
    }

    private void UpdateBackButtonVisibility()
    {
        try
        {
            if (Current == null)
            {
                IsBackVisible = false;
                return;
            }

            var currentState = Current.CurrentState;
            if (currentState == null)
            {
                IsBackVisible = false;
                return;
            }

            var currentRoute = currentState.Location?.OriginalString?.Split('/').LastOrDefault() ?? string.Empty;
            IsBackVisible = !RootRoutes.Contains(currentRoute);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ UpdateBackButtonVisibility failed: {ex}");
            IsBackVisible = false;
        }
    }

    // ========== CLICKED EVENT HANDLERS (used in XAML) ==========

    private async void OnBackClicked(object sender, EventArgs e)
    {
        try
        {
            await GoToAsync("..");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Back navigation failed: {ex.Message}");
            // Fallback to the correct dashboard
            await GoToAsync(GetDashboardRoute());
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
        UpdateUserRoleVisibility();   // hides flyout items via bindings
        await GoToAsync(Routes.Login);
    }

    // ---- Command-based methods (kept for compatibility) ----
    private async void OnBack()
    {
        try
        {
            await GoToAsync("..");
        }
        catch
        {
            await GoToAsync(GetDashboardRoute());
        }
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
        await GoToAsync(Routes.Login);
    }

    // ---- Notification badge update ----
    public void LoadUnreadCount()
    {
        if (App.CurrentUser == null || App.Database == null)
        {
            UnreadCount = 0;
            return;
        }

        try
        {
            Task.Run(async () =>
            {
                var notifications = await App.Database.GetNotificationsForUserAsync(App.CurrentUser.Email);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UnreadCount = notifications.Count(n => !n.IsRead);
                });
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

    // ---- Role visibility ----
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

    // ---- Navigation permission checks ----
    // Include all valid route names (as defined in Routes.cs and ShellContent)
    private readonly HashSet<string> _validRoutes = new()
    {
        "LoginPage", "ForgotPasswordPage",
        "MyNotifications",
        "AdminScanner", "PersonnelScanner",
        "TransferPage", "ClearancePage", "ProfilePage",
        "AdminEquipmentCategory", "PersonnelEquipmentCategory",  // ← NEW
        "CategoryItemsPage",
        "EquipmentDetailPage", "EquipmentRequestDetailPage",
        "ReportDamagePage", "IcsPage", "TransactionHistoryPage",
        "UserManagementPage",
        "AddEquipmentPage",
        "PendingRequestsPage",
        "AuditLogPage",
        "DisposalRequestsPage",
        "AdminDashboard",
        "PersonnelDashboard"
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
            "MyNotifications",
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
                // Fallback to the correct dashboard
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