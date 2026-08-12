using Firetrack.ViewModels;

namespace Firetrack.Views;

public partial class AuditLogPage : ContentPage
{
    public AuditLogPage()
    {
        InitializeComponent();
        BindingContext = new AuditLogViewModel();
    }
}