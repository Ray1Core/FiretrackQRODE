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

    // ---- Commands (kept for other uses) ----
    public ICommand BackCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand GoToNotificationsCommand { get; }

    public new event PropertyChangedEventHandler? PropertyChanged;

    protected new void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ========== ✅ UPDATED RootRoutes ==========
    private static readonly HashSet<string> RootRoutes = new()
    {
        "AdminDashboard",
        "PersonnelDashboard",
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

    // ---- Constructor ----
    public AppShell()
    {
        try
        {
            InitializeComponent();

            // Bind the TitleView Grid to this Shell for IsBackVisible binding
            TitleViewGrid.BindingContext = this;

            // Commands (kept for other uses)
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

    // ========== CLICKED EVENT HANDLERS ==========

    private async void OnBackClicked(object sender, EventArgs e)
    {
        try
        {
            await GoToAsync("..");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Back navigation failed: {ex.Message}");
            await GoToAsync(Routes.Dashboard);
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
        RefreshFlyoutItems();  // ← ADDED: Rebuild flyout after logout
        await GoToAsync(Routes.Login);
    }

    // ---- Original OnBack and OnLogout (kept for command compatibility) ----
    private async void OnBack()
    {
        try
        {
            await GoToAsync("..");
        }
        catch
        {
            await GoToAsync(Routes.Dashboard);
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
        RefreshFlyoutItems();  // ← ADDED: Rebuild flyout after logout
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

    // ---- Dynamically rebuild flyout items based on role ----
    public void RefreshFlyoutItems()
    {
        try
        {
            // Save hidden pages (Login, ForgotPassword, etc.)
            // Use OfType<ShellContent>() – works correctly despite CA2021 warning.
#pragma warning disable CA2021
            var hiddenItems = this.Items
                .OfType<ShellContent>()
                .Where(content => content.FlyoutItemIsVisible == false)
                .ToList();
#pragma warning restore CA2021

            // Clear all items
            this.Items.Clear();

            var user = App.CurrentUser;
            if (user == null)
            {
                // No user logged in – only show hidden pages (Login, ForgotPassword)
                foreach (var hidden in hiddenItems)
                {
                    this.Items.Add(hidden);
                }
                return;
            }

            if (user.Role == "Admin")
            {
                // Create Admin Flyout
                var adminFlyout = new FlyoutItem
                {
                    Title = "Admin",
                    FlyoutDisplayOptions = FlyoutDisplayOptions.AsMultipleItems
                };

                adminFlyout.Items.Add(new ShellContent
                {
                    Title = "Dashboard",
                    ContentTemplate = new DataTemplate(typeof(Views.DashboardPage)),
                    Route = "AdminDashboard"
                });
                adminFlyout.Items.Add(new ShellContent
                {
                    Title = "Inventory",
                    ContentTemplate = new DataTemplate(typeof(Views.EquipmentCategoryPage)),
                    Route = "EquipmentCategoryPage"
                });
                adminFlyout.Items.Add(new ShellContent
                {
                    Title = "Transfer",
                    ContentTemplate = new DataTemplate(typeof(Views.TransferPage)),
                    Route = "TransferPage"
                });
                adminFlyout.Items.Add(new ShellContent
                {
                    Title = "Clearance",
                    ContentTemplate = new DataTemplate(typeof(Views.ClearancePage)),
                    Route = "ClearancePage"
                });
                adminFlyout.Items.Add(new ShellContent
                {
                    Title = "User Management",
                    ContentTemplate = new DataTemplate(typeof(Views.UserManagementPage)),
                    Route = "UserManagementPage"
                });
                adminFlyout.Items.Add(new ShellContent
                {
                    Title = "Pending Requests",
                    ContentTemplate = new DataTemplate(typeof(Views.PendingRequestsPage)),
                    Route = "PendingRequestsPage"
                });
                adminFlyout.Items.Add(new ShellContent
                {
                    Title = "Disposal Requests",
                    ContentTemplate = new DataTemplate(typeof(Views.DisposalRequestsPage)),
                    Route = "DisposalRequestsPage"
                });
                adminFlyout.Items.Add(new ShellContent
                {
                    Title = "Scanner",
                    ContentTemplate = new DataTemplate(typeof(Views.ScannerPage)),
                    Route = "ScannerPage"
                });
                adminFlyout.Items.Add(new ShellContent
                {
                    Title = "Audit Log",
                    ContentTemplate = new DataTemplate(typeof(Views.AuditLogPage)),
                    Route = "AuditLogPage"
                });

                this.Items.Add(adminFlyout);
            }
            else if (user.Role == "Personnel")
            {
                // Create Personnel Flyout
                var personnelFlyout = new FlyoutItem
                {
                    Title = "Personnel",
                    FlyoutDisplayOptions = FlyoutDisplayOptions.AsMultipleItems
                };

                personnelFlyout.Items.Add(new ShellContent
                {
                    Title = "Dashboard",
                    ContentTemplate = new DataTemplate(typeof(Views.DashboardPage)),
                    Route = "PersonnelDashboard"
                });
                personnelFlyout.Items.Add(new ShellContent
                {
                    Title = "Request Equipment",
                    ContentTemplate = new DataTemplate(typeof(Views.EquipmentCategoryPage)),
                    Route = "EquipmentCategoryPage"
                });
                personnelFlyout.Items.Add(new ShellContent
                {
                    Title = "Profile",
                    ContentTemplate = new DataTemplate(typeof(Views.ProfilePage)),
                    Route = "ProfilePage"
                });
                personnelFlyout.Items.Add(new ShellContent
                {
                    Title = "Notifications",
                    ContentTemplate = new DataTemplate(typeof(Views.NotificationsPage)),
                    Route = "NotificationsPage"
                });
                personnelFlyout.Items.Add(new ShellContent
                {
                    Title = "Scanner",
                    ContentTemplate = new DataTemplate(typeof(Views.ScannerPage)),
                    Route = "ScannerPage"
                });

                this.Items.Add(personnelFlyout);
            }

            // Re-add hidden pages (Login, ForgotPassword, etc.)
            foreach (var hidden in hiddenItems)
            {
                this.Items.Add(hidden);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ RefreshFlyoutItems error: {ex}");
        }
    }

    // ---- Navigation permission checks ----
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
            "PersonnelDashboard"
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
                GoToAsync(Routes.Dashboard);
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