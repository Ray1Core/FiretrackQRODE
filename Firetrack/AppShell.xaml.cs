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

    public bool IsBackVisible
    {
        get => _isBackVisible;
        set { _isBackVisible = value; OnPropertyChanged(); }
    }

    public ICommand BackCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand GoToNotificationsCommand { get; }

    public new event PropertyChangedEventHandler? PropertyChanged;

    protected new void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

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
        try
        {
            InitializeComponent();
            BindingContext = this;

            BackCommand = new Command(OnBack);
            LogoutCommand = new Command(OnLogout);
            GoToNotificationsCommand = new Command(async () => await GoToAsync("//NotificationsPage"));

            this.Navigated += OnShellNavigated;

            UpdateUserRoleVisibility();
            LoadUnreadCount();

            // Safe call – will handle null state
            UpdateBackButtonVisibility();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ AppShell constructor error: {ex}");
            // Rethrow so the app fails visibly – but you can also show an alert.
            throw;
        }
    }

    private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        UpdateBackButtonVisibility();
    }

    private void UpdateBackButtonVisibility()
    {
        try
        {
            // 🔥 Critical: check if CurrentState is null (happens before first navigation)
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

    private async void OnBack()
    {
        await GoToAsync("..");
    }

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