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

    // Notification badge – not used in flyout anymore, but keep for future use
    public int UnreadCount
    {
        get => _unreadCount;
        set { _unreadCount = value; OnPropertyChanged(); }
    }

    public bool IsBackVisible
    {
        get => _isBackVisible;
        set { _isBackVisible = value; OnPropertyChanged(); }
    }

    public ICommand BackCommand { get; }
    public ICommand LogoutCommand { get; }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected new void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string GetDashboardRoute()
    {
        var user = App.CurrentUser;
        return user?.Role == "Admin" ? Routes.AdminDashboard : Routes.PersonnelDashboard;
    }

    private static readonly HashSet<string> RootRoutes = new()
    {
        "AdminDashboard",
        "PersonnelDashboard",
        "AdminEquipmentCategory",
        "PersonnelEquipmentCategory",
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

    public AppShell()
    {
        try
        {
            InitializeComponent();

            TitleViewGrid.BindingContext = this;

            BackCommand = new Command(OnBack);
            LogoutCommand = new Command(OnLogout);

            this.Navigated += OnShellNavigated;

            UpdateUserRoleVisibility();
            UpdateBackButtonVisibility();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ AppShell constructor error: {ex}");
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

    private async void OnBackClicked(object sender, EventArgs e)
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
        "LoginPage", "ForgotPasswordPage",
        "MyNotifications",
        "AdminScanner", "PersonnelScanner",
        "TransferPage", "ClearancePage", "ProfilePage",
        "AdminEquipmentCategory", "PersonnelEquipmentCategory",
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