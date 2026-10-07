using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;
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

    public class SoloGameViewModel : ViewModelBase, IPageLifecycle
    {
        private readonly UserEntity _user;
        private readonly ISoloGameService _soloService;
        private readonly DispatcherTimer _playTimer;
        private readonly int _setId;

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

        // Quay lại màn chọn mức độ.
        public event Action? ChangeSetRequested;
        // Về thẳng trang chủ.
        public event Action? HomeRequested;

        public SoloGameViewModel(UserEntity user, int setId)
        {
            _user = user;
            _setId = setId;
            _soloService = ServiceFactory.GetSoloGameService();

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

        // Dừng timer khi vỏ điều hướng đi - tránh rò rỉ.
        public void OnNavigatedFrom()
        {
            _playTimer.Stop();
        }

        private async System.Threading.Tasks.Task InitAsync()
        {
            try
            {
                var (enabled, timeLimit) = await System.Threading.Tasks.Task.Run(
                    () => _soloService.GetSoloModeStatus());
                SoloModeEnabled = enabled;
                TimeLimitSeconds = timeLimit;
            }
            catch (SqlException) { }
            catch (InvalidOperationException) { }

            StartGameWithSet(_setId);
        }

        private void StartGameWithSet(int setId)
        {
            try
            {
                var (ok, message, data) = _soloService.StartGame(setId);
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
            SubmitInternal(isTimeout: false);
        }

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

        private void SubmitInternal(bool isTimeout)
        {
            try
            {
                _playTimer.Stop();
                var selectedIds = GameItems.Where(i => i.IsSelected).Select(i => i.Id).ToList();
                var (ok, message, result) = _soloService.Submit(
                    _user.Id, _setId, selectedIds, _elapsedSeconds, isTimeout);

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
                PlayError = "";
                PlayInfo = isTimeout ? "Hết giờ - bài đã được nộp tự động." : "";
                NotifyGameChanged();
            }
            catch (SqlException) { PlayError = "Lỗi cơ sở dữ liệu khi nộp bài!"; _playTimer.Start(); }
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