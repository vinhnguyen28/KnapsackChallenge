using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using KnapsackChallenge.Core.Factories;
using KnapsackChallenge.Core.Services.Admin;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Shared;

namespace KnapsackChallenge.UI.Features.Admin
{
    public class BanLogViewModel : ViewModelBase
    {
        private readonly IAdminService _adminService;
        private readonly ICollectionView _logsView;

        public ObservableCollection<BanLogEntity> Logs { get; } = new();

        private string _searchText = "";
        private bool _isLoading;
        private string _errorMessage = "";
        private string _infoMessage = "";

        public bool IsLoading
        {
            get => _isLoading;
            private set { _isLoading = value; OnPropertyChanged(); }
        }

        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); _logsView.Refresh(); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public string InfoMessage
        {
            get => _infoMessage;
            set { _infoMessage = value; OnPropertyChanged(); }
        }

        public ICommand RefreshCommand { get; }

        public BanLogViewModel()
        {
            _adminService = ServiceFactory.GetAdminService();
            _logsView = CollectionViewSource.GetDefaultView(Logs);
            _logsView.Filter = o =>
                string.IsNullOrWhiteSpace(SearchText)
                || (o is BanLogEntity log &&
                    (log.TargetUsername?.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) ?? false));

            RefreshCommand = new RelayCommand<object>(_ => _ = LoadAsync());
            _ = LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                var list = await System.Threading.Tasks.Task.Run(() => _adminService.GetBanLogs(null));

                Logs.Clear();
                foreach (var l in list) Logs.Add(l);
                _logsView.Refresh();
            }
            catch (SqlException) { ErrorMessage = "Không kết nối được cơ sở dữ liệu!"; }
            finally { IsLoading = false; }
        }
    }
}