using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;
using KnapsackChallenge.Common.Constants;
using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Factories;
using KnapsackChallenge.Core.Services.Player;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Shared;

namespace KnapsackChallenge.UI.Features.Player
{
    // Wrapper vật phẩm trên lưới "Chơi".
    public class SelectableItem : ViewModelBase
    {
        public int Id { get; }
        public string Name { get; }
        public int Weight { get; }
        public int Value { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
        }

        private bool _isOptimal;
        public bool IsOptimal
        {
            get => _isOptimal;
            set { if (_isOptimal == value) return; _isOptimal = value; OnPropertyChanged(); }
        }

        public SelectableItem(ItemDto item)
        {
            Id = item.Id;
            Name = item.Name;
            Weight = item.Weight;
            Value = item.Value;
        }
    }

    // Lựa chọn filter cho leaderboard.
    public class LeaderboardFilterOption
    {
        public int? SetId { get; init; }
        public string DisplayText { get; init; } = "";
    }

    public class SoloGameViewModel : ViewModelBase
    {
        private readonly UserEntity _user;
        private readonly IPlayerSessionService _session;
        private readonly ISoloGameService _soloService;
        private readonly DispatcherTimer _heartbeatTimer;
        private readonly DispatcherTimer _playTimer;

        public string PlayerUsername => _user.Username ?? "";

        // v4: trạng thái chế độ (đọc đầu phiên + cập nhật sau khi StartGame).
        private bool _soloModeEnabled = true;
        public bool SoloModeEnabled
        {
            get => _soloModeEnabled;
            private set
            {
                _soloModeEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SoloModeDisabled));
                OnPropertyChanged(nameof(CanStart));
            }
        }
        public bool SoloModeDisabled => !_soloModeEnabled;

        public SoloGameViewModel(UserEntity user)
        {
            _user = user;
            _session = ServiceFactory.GetPlayerSessionService();
            _soloService = ServiceFactory.GetSoloGameService();

            _heartbeatTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(AppConfig.HeartbeatIntervalSeconds)
            };
            _heartbeatTimer.Tick += OnHeartbeat;
            _heartbeatTimer.Start();

            _playTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _playTimer.Tick += OnPlayTick;

            LogoutCommand = new RelayCommand<object>(_ => ExecuteLogout());
            StartGameCommand = new RelayCommand<object>(_ => ExecuteStartGame(), _ => CanStart);
            ToggleItemCommand = new RelayCommand<SelectableItem>(ToggleItem);
            ResetCommand = new RelayCommand<object>(_ => ExecuteReset(), _ => HasGame);
            SubmitCommand = new RelayCommand<object>(_ => ExecuteSubmit(), _ => CanSubmit);
            PlayAgainCommand = new RelayCommand<object>(_ => ExecuteReset());
            ChooseAnotherSetCommand = new RelayCommand<object>(_ => ExecuteChooseAnotherSet());
            RefreshLeaderboardCommand = new RelayCommand<object>(_ => _ = LoadLeaderboardAsync());

            _ = LoadSetsAsync();
            _ = LoadSoloModeStatusAsync();
        }

        // View gọi khi UserControl bị unload (đóng app / điều hướng đi).
        public void GoOffline()
        {
            _heartbeatTimer.Stop();
            _playTimer.Stop();
            try { _session.GoOffline(_user.Id); }
            catch (SqlException) { }
            catch (InvalidOperationException) { }
        }

        private void OnHeartbeat(object? sender, EventArgs e)
        {
            try
            {
                var (banned, reason) = _session.CheckStatus(_user.Id);
                if (banned)
                {
                    _heartbeatTimer.Stop();
                    _playTimer.Stop();
                    MessageBox.Show(
                        $"Tài khoản của bạn đã bị khóa. Lý do: {reason ?? "(không có)"}\n" +
                        "Bạn sẽ được đưa về màn đăng nhập.",
                        "Tài khoản bị khóa",
                        MessageBoxButton.OK, MessageBoxImage.Warning);

                    LogoutRequested?.Invoke();
                    return;
                }
                _session.Heartbeat(_user.Id);
            }
            catch (SqlException) { }
            catch (InvalidOperationException) { }
        }

        // =========================================================
        // TAB SELECTION
        // =========================================================
        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set { _selectedTabIndex = value; OnPropertyChanged(); }
        }

        // =========================================================
        // TAB "CHƠI"
        // =========================================================
        public ObservableCollection<SoloGameSetDto> AvailableSets { get; } = new();
        public ObservableCollection<SelectableItem> GameItems { get; } = new();

        private SoloGameSetDto? _selectedSet;
        public SoloGameSetDto? SelectedSet
        {
            get => _selectedSet;
            set
            {
                if (ReferenceEquals(_selectedSet, value)) return;
                _selectedSet = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStart));
            }
        }

        private int _maxWeight;
        public int MaxWeight
        {
            get => _maxWeight;
            private set { _maxWeight = value; OnPropertyChanged(); OnPropertyChanged(nameof(CapacityText)); }
        }

        // v4: giới hạn thời gian chế độ Solo (0 = không giới hạn).
        private int _timeLimitSeconds;
        public int TimeLimitSeconds
        {
            get => _timeLimitSeconds;
            private set
            {
                _timeLimitSeconds = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCountdown));
                OnPropertyChanged(nameof(TimerLabel));
                OnPropertyChanged(nameof(TimerText));
            }
        }
        public bool IsCountdown => TimeLimitSeconds > 0;

        private int _elapsedSeconds;
        public int RemainingSeconds => IsCountdown ? Math.Max(0, TimeLimitSeconds - _elapsedSeconds) : 0;

        public string TimerText => IsCountdown
            ? TimeSpan.FromSeconds(RemainingSeconds).ToString(@"mm\:ss")
            : TimeSpan.FromSeconds(_elapsedSeconds).ToString(@"mm\:ss");

        public string TimerLabel => IsCountdown ? "Còn lại" : "Thời gian";

        private SoloResultDto? _result;
        public SoloResultDto? Result
        {
            get => _result;
            private set
            {
                _result = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasResult));
                OnPropertyChanged(nameof(HasNoResult));
            }
        }

        public bool HasResult => _result != null;
        public bool HasNoResult => _result == null;
        public bool HasGame => GameItems.Count > 0;

        private string _playInfo = "";
        public string PlayInfo
        {
            get => _playInfo;
            set { _playInfo = value; OnPropertyChanged(); }
        }

        private string _playError = "";
        public string PlayError
        {
            get => _playError;
            set { _playError = value; OnPropertyChanged(); }
        }

        private bool _isLoadingSets;
        public bool IsLoadingSets
        {
            get => _isLoadingSets;
            private set { _isLoadingSets = value; OnPropertyChanged(); }
        }

        public int TotalSelectedWeight => GameItems.Where(i => i.IsSelected).Sum(i => i.Weight);
        public int TotalSelectedValue => GameItems.Where(i => i.IsSelected).Sum(i => i.Value);
        public int SelectedCount => GameItems.Count(i => i.IsSelected);
        public bool IsOverWeight => MaxWeight > 0 && TotalSelectedWeight > MaxWeight;

        public double CapacityPercent =>
            MaxWeight <= 0 ? 0 : (double)TotalSelectedWeight / MaxWeight * 100.0;

        public string CapacityText =>
            $"{TotalSelectedWeight} / {MaxWeight} khối lượng  •  {TotalSelectedValue} giá trị  •  {SelectedCount} vật phẩm";

        // v4: chặn StartGame khi chế độ Solo bị tắt.
        public bool CanStart => SoloModeEnabled && SelectedSet != null && !HasGame;

        public bool CanSubmit =>
            HasGame && !HasResult && SelectedCount > 0 && !IsOverWeight;

        // =========================================================
        // TAB "LỊCH SỬ"
        // =========================================================
        public ObservableCollection<GameHistoryDto> History { get; } = new();

        private bool _isLoadingHistory;
        public bool IsLoadingHistory
        {
            get => _isLoadingHistory;
            private set { _isLoadingHistory = value; OnPropertyChanged(); }
        }

        // =========================================================
        // TAB "BẢNG XẾP HẠNG"
        // =========================================================
        public ObservableCollection<LeaderboardEntryDto> Leaderboard { get; } = new();
        public ObservableCollection<LeaderboardFilterOption> LeaderboardFilters { get; } = new();

        private LeaderboardFilterOption? _selectedLeaderboardFilter;
        public LeaderboardFilterOption? SelectedLeaderboardFilter
        {
            get => _selectedLeaderboardFilter;
            set
            {
                if (ReferenceEquals(_selectedLeaderboardFilter, value)) return;
                _selectedLeaderboardFilter = value;
                OnPropertyChanged();
                _ = LoadLeaderboardAsync();
            }
        }

        private bool _isLoadingLeaderboard;
        public bool IsLoadingLeaderboard
        {
            get => _isLoadingLeaderboard;
            private set { _isLoadingLeaderboard = value; OnPropertyChanged(); }
        }

        // =========================================================
        // COMMANDS
        // =========================================================
        public ICommand LogoutCommand { get; }
        public ICommand StartGameCommand { get; }
        public ICommand ToggleItemCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand SubmitCommand { get; }
        public ICommand PlayAgainCommand { get; }
        public ICommand ChooseAnotherSetCommand { get; }
        public ICommand RefreshLeaderboardCommand { get; }

        public event Action? LogoutRequested;

        // =========================================================
        // LOGIC TAB "CHƠI"
        // =========================================================

        // v4: đọc trạng thái chế độ Solo để hiện banner.
        private async System.Threading.Tasks.Task LoadSoloModeStatusAsync()
        {
            try
            {
                var (enabled, timeLimit) = await System.Threading.Tasks.Task.Run(
                    () => _soloService.GetSoloModeStatus());

                SoloModeEnabled = enabled;
                TimeLimitSeconds = timeLimit; // áp dụng cho ván kế tiếp (StartGame sẽ ghi đè lại)
            }
            catch (SqlException) { /* giữ mặc định enabled = true, để UI vẫn dùng được */ }
            catch (InvalidOperationException) { }
        }

        private async System.Threading.Tasks.Task LoadSetsAsync()
        {
            if (IsLoadingSets) return;
            IsLoadingSets = true;
            try
            {
                var sets = await System.Threading.Tasks.Task.Run(() => _soloService.GetAvailableSets());

                AvailableSets.Clear();
                foreach (var s in sets) AvailableSets.Add(s);

                LeaderboardFilters.Clear();
                LeaderboardFilters.Add(new LeaderboardFilterOption
                {
                    SetId = null,
                    DisplayText = "Tất cả bộ đề",
                });
                foreach (var s in sets)
                {
                    LeaderboardFilters.Add(new LeaderboardFilterOption
                    {
                        SetId = s.SetId,
                        DisplayText = s.DisplayText,
                    });
                }
                _selectedLeaderboardFilter = LeaderboardFilters.FirstOrDefault();
                OnPropertyChanged(nameof(SelectedLeaderboardFilter));
            }
            catch (SqlException) { PlayError = "Không kết nối được cơ sở dữ liệu!"; }
            catch (InvalidOperationException ex) { PlayError = ex.Message; }
            finally { IsLoadingSets = false; }
        }

        private void ExecuteStartGame()
        {
            if (!CanStart) return;

            try
            {
                var (ok, message, data) = _soloService.StartGame(SelectedSet!.SetId);
                if (!ok || data == null)
                {
                    PlayError = message;
                    // Nếu lý do là chế độ đóng -> cập nhật banner ngay.
                    if (message.Contains("tạm đóng", StringComparison.OrdinalIgnoreCase))
                        SoloModeEnabled = false;
                    return;
                }

                GameItems.Clear();
                foreach (var it in data.Items)
                    GameItems.Add(new SelectableItem(it));

                MaxWeight = data.MaxWeight;
                TimeLimitSeconds = data.TimeLimitSeconds;
                Result = null;
                PlayError = "";
                PlayInfo = $"Đã bắt đầu bộ đề \"{data.SetName}\".";

                _elapsedSeconds = 0;
                OnPropertyChanged(nameof(TimerText));
                OnPropertyChanged(nameof(RemainingSeconds));
                _playTimer.Start();

                NotifyGameChanged();
            }
            catch (SqlException) { PlayError = "Lỗi cơ sở dữ liệu khi tải bộ đề!"; }
        }

        private void ToggleItem(SelectableItem? item)
        {
            if (item == null || HasResult) return;
            item.IsSelected = !item.IsSelected;
            NotifyGameChanged();
        }

        private void ExecuteReset()
        {
            if (GameItems.Count == 0) return;

            foreach (var it in GameItems)
            {
                it.IsSelected = false;
                it.IsOptimal = false;
            }
            Result = null;
            PlayInfo = "";
            PlayError = "";

            _elapsedSeconds = 0;
            OnPropertyChanged(nameof(TimerText));
            OnPropertyChanged(nameof(RemainingSeconds));
            _playTimer.Start();

            NotifyGameChanged();
        }

        private void ExecuteChooseAnotherSet()
        {
            _playTimer.Stop();
            GameItems.Clear();
            Result = null;
            MaxWeight = 0;
            SelectedSet = null;
            PlayInfo = "";
            PlayError = "";
            _elapsedSeconds = 0;
            OnPropertyChanged(nameof(TimerText));
            OnPropertyChanged(nameof(RemainingSeconds));
            NotifyGameChanged();
        }

        private void ExecuteSubmit()
        {
            if (!CanSubmit) return;
            SubmitInternal(isTimeout: false);
        }

        // v4: đồng hồ đếm tick 1s - xử lý đếm ngược + auto submit.
        private void OnPlayTick(object? sender, EventArgs e)
        {
            _elapsedSeconds++;
            OnPropertyChanged(nameof(TimerText));
            OnPropertyChanged(nameof(RemainingSeconds));

            if (IsCountdown && _elapsedSeconds >= TimeLimitSeconds && !HasResult)
            {
                _playTimer.Stop();
                SubmitInternal(isTimeout: true);
            }
        }

        // Nộp bài chung cho cả 2 luồng (chủ động & timeout).
        private void SubmitInternal(bool isTimeout)
        {
            try
            {
                _playTimer.Stop();
                var selectedIds = GameItems.Where(i => i.IsSelected).Select(i => i.Id).ToList();
                var (ok, message, result) = _soloService.Submit(
                    _user.Id, SelectedSet!.SetId, selectedIds, _elapsedSeconds, isTimeout);

                if (!ok || result == null)
                {
                    PlayError = isTimeout ? ("Hết giờ. " + message) : message;
                    if (!message.Contains("tạm đóng", StringComparison.OrdinalIgnoreCase))
                        _playTimer.Start(); // tiếp tục đếm nếu không phải do bị tắt
                    else
                        SoloModeEnabled = false;
                    return;
                }

                var optimalSet = result.OptimalItemIds.ToHashSet();
                foreach (var it in GameItems)
                    it.IsOptimal = optimalSet.Contains(it.Id);

                Result = result;
                PlayError = "";
                PlayInfo = isTimeout ? "Hết giờ - bài đã được nộp tự động." : "";
                NotifyGameChanged();
            }
            catch (SqlException) { PlayError = "Lỗi cơ sở dữ liệu khi nộp bài!"; _playTimer.Start(); }
        }

        private void ExecuteLogout()
        {
            _playTimer.Stop();
            try { _session.GoOffline(_user.Id); }
            catch (SqlException) { }
            catch (InvalidOperationException) { }

            LogoutRequested?.Invoke();
        }

        private void NotifyGameChanged()
        {
            OnPropertyChanged(nameof(HasGame));
            OnPropertyChanged(nameof(TotalSelectedWeight));
            OnPropertyChanged(nameof(TotalSelectedValue));
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(IsOverWeight));
            OnPropertyChanged(nameof(CapacityPercent));
            OnPropertyChanged(nameof(CapacityText));
            OnPropertyChanged(nameof(CanSubmit));
            OnPropertyChanged(nameof(CanStart));
        }

        // =========================================================
        // TAB "LỊCH SỬ"
        // =========================================================
        public async System.Threading.Tasks.Task LoadHistoryAsync()
        {
            if (IsLoadingHistory) return;
            IsLoadingHistory = true;
            try
            {
                var list = await System.Threading.Tasks.Task.Run(
                    () => _soloService.GetHistory(_user.Id, 50));

                History.Clear();
                foreach (var h in list) History.Add(h);
            }
            catch (SqlException) { }
            finally { IsLoadingHistory = false; }
        }

        // =========================================================
        // TAB "BẢNG XẾP HẠNG"
        // =========================================================
        private async System.Threading.Tasks.Task LoadLeaderboardAsync()
        {
            if (IsLoadingLeaderboard) return;
            IsLoadingLeaderboard = true;
            try
            {
                int? setId = _selectedLeaderboardFilter?.SetId;
                var list = await System.Threading.Tasks.Task.Run(
                    () => _soloService.GetLeaderboard(setId, 20));

                Leaderboard.Clear();
                foreach (var e in list) Leaderboard.Add(e);
            }
            catch (SqlException) { }
            finally { IsLoadingLeaderboard = false; }
        }
    }
}