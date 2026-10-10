using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using KnapsackChallenge.Core.Factories;
using KnapsackChallenge.Core.Services.Admin;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Shared;

namespace KnapsackChallenge.UI.Features.Admin
{
    public class GameModeRowViewModel : ViewModelBase
    {
        public string ModeKey { get; }
        public string DisplayName { get; }
        public bool OriginalIsEnabled { get; private set; }

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        private string _timeLimitText = "0";
        public string TimeLimitText
        {
            get => _timeLimitText;
            set { _timeLimitText = value; OnPropertyChanged(); }
        }

        private string _maxPlayersText = "";
        public string MaxPlayersText
        {
            get => _maxPlayersText;
            set { _maxPlayersText = value; OnPropertyChanged(); }
        }

        // v7: cấu hình tim (chỉ Solo)
        private string _maxHeartsText = "";
        public string MaxHeartsText
        {
            get => _maxHeartsText;
            set { _maxHeartsText = value; OnPropertyChanged(); }
        }

        private string _heartRefillMinutesText = "";
        public string HeartRefillMinutesText
        {
            get => _heartRefillMinutesText;
            set { _heartRefillMinutesText = value; OnPropertyChanged(); }
        }

        private string _updatedInfoText = "";
        public string UpdatedInfoText
        {
            get => _updatedInfoText;
            private set { _updatedInfoText = value; OnPropertyChanged(); }
        }

        public bool IsMultiplayer =>
            string.Equals(ModeKey, "Multiplayer", StringComparison.OrdinalIgnoreCase);

        public bool IsSolo =>
            string.Equals(ModeKey, "Solo", StringComparison.OrdinalIgnoreCase);

        public string ModeKeyBadge => ModeKey.ToUpperInvariant();

        public GameModeRowViewModel(GameModeEntity e)
        {
            ModeKey = e.ModeKey;
            DisplayName = e.DisplayName;
            ApplyFromEntity(e);
        }

        public void ApplyFromEntity(GameModeEntity e)
        {
            OriginalIsEnabled = e.IsEnabled;
            IsEnabled = e.IsEnabled;
            TimeLimitText = e.TimeLimitSeconds.ToString();
            MaxPlayersText = e.MaxPlayers?.ToString() ?? "";
            MaxHeartsText = e.MaxHearts?.ToString() ?? "";
            HeartRefillMinutesText = e.HeartRefillMinutes?.ToString() ?? "";
            UpdatedInfoText = $"Cập nhật cuối: {e.UpdatedAt.ToLocalTime():dd/MM/yyyy HH:mm}" +
                              (string.IsNullOrEmpty(e.UpdatedBy) ? "" : $" bởi {e.UpdatedBy}");
        }
    }

    public class GameModeManagementViewModel : ViewModelBase, IPageLifecycle
    {
        private readonly IGameModeService _service;
        private readonly IDialogService _dialog;

        public ObservableCollection<GameModeRowViewModel> Modes { get; } = new();

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set { _isLoading = value; OnPropertyChanged(); }
        }

        private string _errorMessage = "";
        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        private string _infoMessage = "";
        public string InfoMessage
        {
            get => _infoMessage;
            set { _infoMessage = value; OnPropertyChanged(); }
        }

        public ICommand RefreshCommand { get; }
        public ICommand SaveCommand { get; }

        public GameModeManagementViewModel()
        {
            _service = ServiceFactory.GetGameModeService();
            _dialog = new WpfDialogService();

            RefreshCommand = new RelayCommand<object>(_ => _ = LoadAsync());
            SaveCommand = new RelayCommand<GameModeRowViewModel>(row => _ = SaveAsync(row));

            _ = LoadAsync();
        }

        public void OnNavigatedFrom() { }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                var list = await System.Threading.Tasks.Task.Run(() => _service.GetAll());

                Modes.Clear();
                foreach (var e in list)
                    Modes.Add(new GameModeRowViewModel(e));

                ErrorMessage = "";
                InfoMessage = "";
            }
            catch (SqlException) { ErrorMessage = "Không kết nối được cơ sở dữ liệu!"; }
            catch (InvalidOperationException ex) { ErrorMessage = ex.Message; }
            finally { IsLoading = false; }
        }

        private async System.Threading.Tasks.Task SaveAsync(GameModeRowViewModel? row)
        {
            if (row == null) return;

            ErrorMessage = "";
            InfoMessage = "";

            if (row.OriginalIsEnabled && !row.IsEnabled)
            {
                if (!_dialog.Confirm(
                        $"Tắt chế độ \"{row.DisplayName}\"?\nNgười chơi sẽ không thể bắt đầu ván mới ở chế độ này.",
                        "Xác nhận tắt chế độ"))
                    return;
            }

            if (!int.TryParse(row.TimeLimitText, out int timeLimit) || timeLimit < 0)
            {
                ErrorMessage = "Giới hạn thời gian phải là số nguyên không âm!";
                return;
            }

            int? maxPlayers = null;
            if (row.IsMultiplayer && !string.IsNullOrWhiteSpace(row.MaxPlayersText))
            {
                if (!int.TryParse(row.MaxPlayersText, out int mp))
                {
                    ErrorMessage = "Số người tối đa phải là số nguyên!";
                    return;
                }
                maxPlayers = mp;
            }

            // v7: cấu hình tim (chỉ Solo).
            int? maxHearts = null;
            int? heartRefillMinutes = null;
            if (row.IsSolo)
            {
                if (!string.IsNullOrWhiteSpace(row.MaxHeartsText))
                {
                    if (!int.TryParse(row.MaxHeartsText, out int mh))
                    {
                        ErrorMessage = "Số tim tối đa phải là số nguyên!";
                        return;
                    }
                    maxHearts = mh;
                }
                if (!string.IsNullOrWhiteSpace(row.HeartRefillMinutesText))
                {
                    if (!int.TryParse(row.HeartRefillMinutesText, out int rm))
                    {
                        ErrorMessage = "Thời gian hồi tim phải là số nguyên!";
                        return;
                    }
                    heartRefillMinutes = rm;
                }
            }

            try
            {
                var (ok, message) = await System.Threading.Tasks.Task.Run(() =>
                {
                    var adminStub = new UserEntity { Username = _currentAdminUsername };
                    return _service.Update(row.ModeKey, row.IsEnabled, timeLimit,
                                            maxPlayers, maxHearts, heartRefillMinutes, adminStub);
                });

                if (ok)
                {
                    InfoMessage = message;
                    await LoadAsync();
                }
                else
                {
                    ErrorMessage = message;
                }
            }
            catch (SqlException) { ErrorMessage = "Lỗi cơ sở dữ liệu khi lưu cấu hình!"; }
        }

        private string _currentAdminUsername = "";
        public void SetCurrentAdmin(string username) => _currentAdminUsername = username ?? "";
    }
}