using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Services.Player;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Services;
using KnapsackChallenge.UI.Shared;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;

namespace KnapsackChallenge.UI.Features.Player
{
    // Wrapper vật phẩm trên lưới "Chơi".
    public class SelectableItem : ViewModelBase
    {
        public int Id { get; }
        public string Name { get; }
        public int Weight { get; }
        public int Value { get; }

        // v10: khoá icon — copy từ ItemDto để UI có thể bind ItemIconConverter.
        // NULL = dùng icon "default".
        public string? IconKey { get; }

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
            IconKey = item.IconKey; // v10
        }
    }

    public class SoloGameViewModel : ViewModelBase, IPageLifecycle
    {
        private readonly UserEntity _user;
        private readonly ISoloGameService _soloService;
        private readonly DispatcherTimer _playTimer;
        private readonly int _setId;

        // Cờ chống bấm đúp / tick chồng.
        private bool _startGameBusy;
        private bool _submitBusy;

        // v7: báo cho vỏ Player biết tim đã thay đổi (sau Start / hồi).
        public event Action<HeartStatusDto>? HeartsChanged;

        // v8: báo cho vỏ Player khi EXP đã được cộng.
        public event Action<ExpAwardResultDto>? ExpAwarded;

        private string _currentSetName = "";
        public string CurrentSetName
        {
            get => _currentSetName;
            private set { _currentSetName = value; OnPropertyChanged(); }
        }

        private string _currentDifficulty = "";
        public string CurrentDifficulty
        {
            get => _currentDifficulty;
            private set { _currentDifficulty = value; OnPropertyChanged(); }
        }

        public event Action? ChangeSetRequested;
        public event Action? HomeRequested;

        // v7: hiển thị tim ngay trong màn chơi khi hết tim.
        private HeartStatusDto? _heartStatus;
        public HeartStatusDto? HeartStatus
        {
            get => _heartStatus;
            private set
            {
                _heartStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HeartBadgeText));
                OnPropertyChanged(nameof(HeartCountdownText));
                OnPropertyChanged(nameof(HasHeartStatus));
            }
        }
        public bool HasHeartStatus => _heartStatus != null;
        public string HeartBadgeText => _heartStatus?.BadgeText ?? "❤ --/--";
        public string HeartCountdownText =>
            _heartStatus?.IsEmpty == true
                ? $"Hồi tim sau {_heartStatus.CountdownText}"
                : "";

        public SoloGameViewModel(UserEntity user, int setId)
        {
            _user = user;
            _setId = setId;
            _soloService = ClientServiceFactory.GetSoloGameService();

            _playTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _playTimer.Tick += OnPlayTick;

            ResetCommand = new RelayCommand<object>(_ => ExecuteReset(), _ => HasGame);
            SubmitCommand = new RelayCommand<object>(_ => ExecuteSubmit(), _ => CanSubmit);
            ToggleItemCommand = new RelayCommand<SelectableItem>(ToggleItem);
            PlayAgainCommand = new RelayCommand<object>(_ => ExecuteReset());
            ChooseAnotherSetCommand = new RelayCommand<object>(_ => ExecuteChooseAnotherSet());
            GoHomeCommand = new RelayCommand<object>(_ => ExecuteGoHome());

            _ = InitAsync();
        }

        public ObservableCollection<SelectableItem> GameItems { get; } = new();

        private int _maxWeight;
        public int MaxWeight
        {
            get => _maxWeight;
            private set { _maxWeight = value; OnPropertyChanged(); OnPropertyChanged(nameof(CapacityText)); }
        }

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

        private bool _soloModeEnabled = true;
        public bool SoloModeEnabled
        {
            get => _soloModeEnabled;
            private set
            {
                _soloModeEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SoloModeDisabled));
            }
        }
        public bool SoloModeDisabled => !_soloModeEnabled;

        public int TotalSelectedWeight => GameItems.Where(i => i.IsSelected).Sum(i => i.Weight);
        public int TotalSelectedValue => GameItems.Where(i => i.IsSelected).Sum(i => i.Value);
        public int SelectedCount => GameItems.Count(i => i.IsSelected);
        public bool IsOverWeight => MaxWeight > 0 && TotalSelectedWeight > MaxWeight;

        public double CapacityPercent =>
            MaxWeight <= 0 ? 0 : (double)TotalSelectedWeight / MaxWeight * 100.0;

        public string CapacityText =>
            $"{TotalSelectedWeight} / {MaxWeight} khối lượng  •  {TotalSelectedValue} giá trị  •  {SelectedCount} vật phẩm";

        public bool CanSubmit =>
            HasGame && !HasResult && SelectedCount > 0 && !IsOverWeight;

        public ICommand ToggleItemCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand SubmitCommand { get; }
        public ICommand PlayAgainCommand { get; }
        public ICommand ChooseAnotherSetCommand { get; }
        public ICommand GoHomeCommand { get; }

        public void OnNavigatedFrom() => _playTimer.Stop();

        private async System.Threading.Tasks.Task InitAsync()
        {
            bool serverUnavailable = false;
            try
            {
                var (enabled, timeLimit) = await System.Threading.Tasks.Task.Run(
                    () => _soloService.GetSoloModeStatus());
                SoloModeEnabled = enabled;
                TimeLimitSeconds = timeLimit;
            }
            catch (ServerUnavailableException ex)
            {
                // Server tắt: báo rõ và KHÔNG gọi StartGame (tránh thêm 1 lần fail).
                PlayError = ApiErrorText.From(ex);
                serverUnavailable = true;
            }
            catch (Exception)
            {
                // Lỗi khác: giữ giá trị mặc định rồi vẫn tiếp tục StartGame.
            }

            if (serverUnavailable) return;

            await StartGameWithSetAsync(_setId);
        }

        private async System.Threading.Tasks.Task StartGameWithSetAsync(int setId)
        {
            if (_startGameBusy) return;
            _startGameBusy = true;
            try
            {
                bool ok;
                string message;
                SoloGameDataDto? data;
                HeartStatusDto? heart;

                try
                {
                    var tuple = await System.Threading.Tasks.Task.Run(
                        () => _soloService.StartGame(_user.Id, setId));
                    ok = tuple.Success;
                    message = tuple.Message;
                    data = tuple.Data;
                    heart = tuple.HeartStatus;
                }
                catch (Exception ex)
                {
                    PlayError = ApiErrorText.From(ex);
                    return;
                }

                if (heart != null)
                {
                    HeartStatus = heart;
                    HeartsChanged?.Invoke(heart);
                }

                if (!ok || data == null)
                {
                    PlayError = message;
                    if (message.Contains("tạm đóng", StringComparison.OrdinalIgnoreCase))
                        SoloModeEnabled = false;
                    return;
                }

                GameItems.Clear();
                foreach (var it in data.Items)
                    GameItems.Add(new SelectableItem(it));

                MaxWeight = data.MaxWeight;
                TimeLimitSeconds = data.TimeLimitSeconds;
                CurrentSetName = data.SetName;
                CurrentDifficulty = data.Difficulty;
                Result = null;
                PlayError = "";
                PlayInfo = $"Đã bắt đầu bộ đề \"{data.SetName}\".";

                _elapsedSeconds = 0;
                OnPropertyChanged(nameof(TimerText));
                OnPropertyChanged(nameof(RemainingSeconds));
                _playTimer.Start();

                NotifyGameChanged();
            }
            finally
            {
                _startGameBusy = false;
            }
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
            ChangeSetRequested?.Invoke();
        }

        private void ExecuteGoHome()
        {
            _playTimer.Stop();
            HomeRequested?.Invoke();
        }

        private void ExecuteSubmit()
        {
            if (!CanSubmit) return;
            _ = SubmitInternalAsync(isTimeout: false);
        }

        private void OnPlayTick(object? sender, EventArgs e)
        {
            _elapsedSeconds++;
            OnPropertyChanged(nameof(TimerText));
            OnPropertyChanged(nameof(RemainingSeconds));

            if (IsCountdown && _elapsedSeconds >= TimeLimitSeconds && !HasResult)
            {
                _playTimer.Stop();
                _ = SubmitInternalAsync(isTimeout: true);
            }
        }

        private async System.Threading.Tasks.Task SubmitInternalAsync(bool isTimeout)
        {
            if (_submitBusy) return;
            _submitBusy = true;
            try
            {
                _playTimer.Stop();
                var selectedIds = GameItems.Where(i => i.IsSelected).Select(i => i.Id).ToList();

                bool ok;
                string message;
                SoloResultDto? result;

                try
                {
                    var tuple = await System.Threading.Tasks.Task.Run(
                        () => _soloService.Submit(
                            _user.Id, _setId, selectedIds, _elapsedSeconds, isTimeout));
                    ok = tuple.Success;
                    message = tuple.Message;
                    result = tuple.Result;
                }
                catch (Exception ex)
                {
                    // Lỗi mạng: báo rõ. Nếu không phải timeout thì mở lại đồng hồ
                    // để người chơi bấm Nộp lại; nếu timeout thì để Nộp thủ công.
                    PlayError = ApiErrorText.From(ex);
                    if (!isTimeout) _playTimer.Start();
                    return;
                }

                if (!ok || result == null)
                {
                    PlayError = isTimeout ? ("Hết giờ. " + message) : message;
                    if (!message.Contains("tạm đóng", StringComparison.OrdinalIgnoreCase))
                        _playTimer.Start();
                    else
                        SoloModeEnabled = false;
                    return;
                }

                var optimalSet = result.OptimalItemIds.ToHashSet();
                foreach (var it in GameItems)
                    it.IsOptimal = optimalSet.Contains(it.Id);

                Result = result;

                if (result.ExpGained > 0 || result.LeveledUp)
                {
                    ExpAwarded?.Invoke(new ExpAwardResultDto
                    {
                        ExpGained = result.ExpGained,
                        NewLevel = result.NewLevel,
                        OldLevel = result.OldLevel,
                        RankTitle = result.RankTitle,
                    });
                }

                PlayError = "";
                PlayInfo = isTimeout ? "Hết giờ - bài đã được nộp tự động." : "";
                NotifyGameChanged();
            }
            finally
            {
                _submitBusy = false;
            }
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
        }
    }
}