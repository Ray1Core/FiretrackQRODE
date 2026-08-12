using Firetrack.Models;
using Firetrack.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class AuditLogViewModel : ViewModelBase
    {
        private ObservableCollection<AuditLogModel> _logs = new();
        private bool _isBusy;

        public ObservableCollection<AuditLogModel> Logs
        {
            get => _logs;
            set { _logs = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand LoadLogsCommand { get; }

        public AuditLogViewModel()
        {
            LoadLogsCommand = new Command(OnLoadLogs);
            OnLoadLogs();
        }

        private async void OnLoadLogs()
        {
            if (App.Database == null) return;
            IsBusy = true;
            try
            {
                var logs = await App.Database.GetAuditLogsAsync();
                Logs.Clear();
                foreach (var log in logs)
                    Logs.Add(log);
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}