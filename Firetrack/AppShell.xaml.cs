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

    // ✅ Added 'new' keyword to hide base class event (CS0108 fix)
    public new event PropertyChangedEventHandler? PropertyChanged;

    // ✅ Added 'new' keyword to hide base class method (CS0114 fix)
    protected new void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public AppShell()
    {
        InitializeComponent();
        BindingContext = this;

        // Set initial visibility based on current user
        UpdateUserRoleVisibility();
    }

    /// <summary>
    /// Updates the flyout visibility based on the current user's role.
    /// Call this method whenever the user logs in or out.
    /// </summary>
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

    /// <summary>
    /// Checks if the current user is allowed to navigate to the given route.
    /// </summary>
    private bool IsRouteAllowed(string route)
    {
        var user = App.CurrentUser;
        bool isLoggedIn = user != null;
        bool isAdmin = isLoggedIn && user!.Role == "Admin";

        // Not logged in → only Login and ForgotPassword are allowed
        if (!isLoggedIn)
            return route == "LoginPage" || route == "ForgotPasswordPage";

        // Logged in → cannot go back to Login or ForgotPassword
        if (route == "LoginPage" || route == "ForgotPasswordPage")
            return false;

        // Admin → all pages allowed
        if (isAdmin)
            return true;

        // Personnel → only these specific pages
        var allowedForPersonnel = new[]
        {
            "DashboardPage",
            "ProfilePage",
            "RequestEquipmentPage",
            "ReportDamagePage",
            "ScannerPage",
            "NotificationsPage",
            "TransactionHistoryPage",
            "IcsPage"
        };

        return allowedForPersonnel.Contains(route);
    }

    /// <summary>
    /// Intercepts every navigation and enforces the access rules.
    /// </summary>
    protected override void OnNavigating(ShellNavigatingEventArgs args)
    {
        base.OnNavigating(args);

        // Extract the target route (last segment after the last '/')
        var target = args.Target.Location.OriginalString;
        var route = target.Split('/').Last();

        System.Diagnostics.Debug.WriteLine($"🛡️ Navigation Guard: Route={route}, User={App.CurrentUser?.Username}, Role={App.CurrentUser?.Role}");

        if (!IsRouteAllowed(route))
        {
            System.Diagnostics.Debug.WriteLine($"🚫 Blocked: {route}");

            // Cancel the forbidden navigation
            args.Cancel();

            // Redirect to a safe page
            if (App.CurrentUser == null)
            {
                // Not logged in → go to Login
                GoToAsync("//LoginPage");
            }
            else
            {
                // Logged in but trying to access an unauthorized page → go to Dashboard
                GoToAsync("//DashboardPage");
                // Optional: show a friendly message
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