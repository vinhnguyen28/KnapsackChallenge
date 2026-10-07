using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;
using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Factories;
using KnapsackChallenge.Core.Services.Admin;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Features.Admin.Dialogs;
using KnapsackChallenge.UI.Shared;

namespace KnapsackChallenge.UI.Features.Admin
{
    // Bộ lọc tab.
    public enum PlayerFilter { All, Online, Offline, Banned }

    // Bọc DTO để bổ sung CanBan/CanUnban phụ thuộc current admin.
    public class PlayerRowViewModel
    {
        public UserListItemDto Data { get; }
        public bool CanBan { get; }
        public bool CanUnban { get; }

        public PlayerRowViewModel(UserListItemDto data, int currentAdminId)
        {
            Data = data;
            CanBan = !data.IsBanned
                     && !string.Equals(data.Role, "Admin", StringComparison.OrdinalIgnoreCase)
                     && data.Id != currentAdminId;
            CanUnban = data.IsBanned;
        }

        public int Id => Data.Id;
        public string Username => Data.Username;
        public string Role => Data.Role;
        public bool IsBanned => Data.IsBanned;
        public bool IsOnline => Data.IsOnline;
        public string StatusText => Data.StatusText;
        public string? BanReason => Data.BanReason;
        public DateTime CreatedAt => Data.CreatedAt;
        public DateTime? LastLoginAt => Data.LastLoginAt;
    }

    public class PlayerManagementViewModel : ViewModelBase, IPageLifecycle
    {
        private readonly IAdminService _adminService;
        private readonly IDialogService _dialog;
        private readonly UserEntity _currentAdmin;
        private readonly DispatcherTimer _refreshTimer;
        private readonly ICollectionView _usersView;

        public ObservableCollection<PlayerRowViewModel> Users { get; } = new();

        private string _searchText = "";
        private PlayerFilter _filter = PlayerFilter.All;
        private bool _isLoading;
        private string _errorMessage = "";
        private string _infoMessage = "";

        private int _totalCount, _onlineCount, _bannedCount, _todayCount;

        public int TotalCount { get => _totalCount; private set { _totalCount = value; OnPropertyChanged(); } }
        public int OnlineCount { get => _onlineCount; private set { _onlineCount = value; OnPropertyChanged(); } }
        public int BannedCount { get => _bannedCount; private set { _bannedCount = value; OnPropertyChanged(); } }
        public int TodayCount { get => _todayCount; private set { _todayCount = value; OnPropertyChanged(); } }

        public bool IsLoading
        {
            get => _isLoading;
            private set { _isLoading = value; OnPropertyChanged(); }
        }

        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); _usersView.Refresh(); }
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

        // --- 4 tab lọc: mỗi thuộc tính bool set _filter rồi refresh view ---
        public bool IsFilterAll
        {
            get => _filter == PlayerFilter.All;
            set { if (value) SetFilter(PlayerFilter.All); }
        }
        public bool IsFilterOnline
        {
            get => _filter == PlayerFilter.Online;
            set { if (value) SetFilter(PlayerFilter.Online); }
        }
        public bool IsFilterOffline
        {
            get => _filter == PlayerFilter.Offline;
            set { if (value) SetFilter(PlayerFilter.Offline); }
        }
        public bool IsFilterBanned
        {
            get => _filter == PlayerFilter.Banned;
            set { if (value) SetFilter(PlayerFilter.Banned); }
        }

        public ICommand RefreshCommand { get; }
        public ICommand ViewAchievementCommand { get; }
        public ICommand BanCommand { get; }
        public ICommand UnbanCommand { get; }

        public PlayerManagementViewModel(UserEntity currentAdmin, IDialogService? dialog = null)
        {
            _currentAdmin = currentAdmin;
            _dialog = dialog ?? new WpfDialogService();
            _adminService = ServiceFactory.GetAdminService();

            _usersView = CollectionViewSource.GetDefaultView(Users);
            _usersView.Filter = FilterUser;

            RefreshCommand = new RelayCommand<object>(_ => _ = LoadAsync());
            ViewAchievementCommand = new RelayCommand<PlayerRowViewModel>(row => _ = ViewAchievementAsync(row));
            BanCommand = new RelayCommand<PlayerRowViewModel>(row => _ = BanAsync(row), row => row?.CanBan == true);
            UnbanCommand = new RelayCommand<PlayerRowViewModel>(row => _ = UnbanAsync(row), row => row?.CanUnban == true);

            // Auto-refresh 15s. Stop khi OnNavigatedFrom để tránh rò rỉ.
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _refreshTimer.Tick += (_, _) => _ = LoadAsync();
            _refreshTimer.Start();

            _ = LoadAsync();
        }

        public void OnNavigatedFrom() => _refreshTimer.Stop();

        private void SetFilter(PlayerFilter f)
        {
            _filter = f;
            OnPropertyChanged(nameof(IsFilterAll));
            OnPropertyChanged(nameof(IsFilterOnline));
            OnPropertyChanged(nameof(IsFilterOffline));
            OnPropertyChanged(nameof(IsFilterBanned));
            _usersView.Refresh();
        }

        private bool FilterUser(object o)
        {
            if (o is not PlayerRowViewModel row) return false;
            var d = row.Data;

            if (!string.IsNullOrWhiteSpace(SearchText) &&
                !d.Username.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;

            return _filter switch
            {
                PlayerFilter.Online => d.IsOnline,
                PlayerFilter.Offline => !d.IsOnline && !d.IsBanned,
                PlayerFilter.Banned => d.IsBanned,
                _ => true,
            };
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                var result = await System.Threading.Tasks.Task.Run(() =>
                {
                    var list = _adminService.GetAllUsers();
                    var stats = _adminService.GetStats();
                    return (list, stats);
                });

                Users.Clear();
                foreach (var u in result.list)
                    Users.Add(new PlayerRowViewModel(u, _currentAdmin.Id));

                TotalCount = result.stats.Total;
                OnlineCount = result.stats.Online;
                BannedCount = result.stats.Banned;
                TodayCount = result.stats.Today;

                _usersView.Refresh();
            }
            catch (SqlException) { ErrorMessage = "Không kết nối được cơ sở dữ liệu!"; }
            catch (InvalidOperationException ex) { ErrorMessage = ex.Message; }
            finally { IsLoading = false; }
        }

        private async System.Threading.Tasks.Task ViewAchievementAsync(PlayerRowViewModel? row)
        {
            if (row == null) return;
            try
            {
                var data = await System.Threading.Tasks.Task.Run(() =>
                    _adminService.GetPlayerAchievement(row.Id));

                if (data == null) { ErrorMessage = "Không tìm thấy người chơi."; return; }

                var dlg = new AchievementDialog(data)
                {
                    Owner = Application.Current.MainWindow
                };
                dlg.ShowDialog();
            }
            catch (SqlException) { ErrorMessage = "Lỗi khi tải thành tích!"; }
        }

        private async System.Threading.Tasks.Task BanAsync(PlayerRowViewModel? row)
        {
            if (row == null || !row.CanBan) return;

            var dlg = new BanDialog(row.Username) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;

            bool ok; string message;
            try
            {
                var r = await System.Threading.Tasks.Task.Run(() =>
                    _adminService.BanUser(row.Id, dlg.BanReason, _currentAdmin));
                ok = r.Success; message = r.Message;
            }
            catch (SqlException) { ok = false; message = "Lỗi cơ sở dữ liệu khi ban!"; }

            if (ok) { InfoMessage = message; await LoadAsync(); }
            else ErrorMessage = message;
        }

        private async System.Threading.Tasks.Task UnbanAsync(PlayerRowViewModel? row)
        {
            if (row == null || !row.CanUnban) return;

            var dlg = new UnbanDialog(row.Username) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;

            bool ok; string message;
            try
            {
                var r = await System.Threading.Tasks.Task.Run(() =>
                    _adminService.UnbanUser(row.Id, dlg.UnbanNote, _currentAdmin));
                ok = r.Success; message = r.Message;
            }
            catch (SqlException) { ok = false; message = "Lỗi cơ sở dữ liệu khi bỏ ban!"; }

            if (ok) { InfoMessage = message; await LoadAsync(); }
            else ErrorMessage = message;
        }
    }
}