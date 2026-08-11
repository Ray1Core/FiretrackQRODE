using Firetrack.Models;
using Firetrack.Services;
using Microsoft.Maui.Controls;

namespace Firetrack
{
    public partial class App : Application
    {
        public static UserModel? CurrentUser { get; set; }
        public static DatabaseService? Database { get; private set; }

        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            string connectionString = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=FiretrackDB;Integrated Security=True;";

            Database = new DatabaseService(connectionString);

            return new Window(new AppShell());
        }
    }
}