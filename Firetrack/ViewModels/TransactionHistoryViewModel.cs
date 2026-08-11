using Firetrack.Models;
using Firetrack.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Firetrack.ViewModels
{
    public class TransactionHistoryViewModel : ViewModelBase
    {
        private ObservableCollection<TransactionModel> _transactions = new();
        private EquipmentModel? _equipment;
        private bool _isBusy;

        public ObservableCollection<TransactionModel> Transactions
        {
            get => _transactions;
            set { _transactions = value; OnPropertyChanged(); }
        }

        public EquipmentModel? Equipment
        {
            get => _equipment;
            set { _equipment = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        // GoBackCommand removed – navigation handled by Shell

        public TransactionHistoryViewModel(EquipmentModel equipment)
        {
            Equipment = equipment;
            // GoBackCommand assignment removed
            LoadTransactions();
        }

        private async void LoadTransactions()
        {
            if (App.Database == null || Equipment == null) return;

            IsBusy = true;
            try
            {
                var logs = await App.Database.GetTransactionsForEquipmentAsync(Equipment.QRCode);
                Transactions.Clear();
                foreach (var t in logs.OrderByDescending(t => t.Timestamp))
                    Transactions.Add(t);
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