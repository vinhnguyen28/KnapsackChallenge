using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Services.Player;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Services;
using KnapsackChallenge.UI.Shared;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace KnapsackChallenge.UI.Features.Player
{
    // Trang Xếp hạng (v9): 2 tab — Cấp độ (EXP) và Theo bộ đề.
    // Tab EXP: top 100 toàn hệ thống + hạng của tôi.
    // Tab bộ đề: lọc theo độ khó / bộ đề cụ thể / chế độ / thời gian + hạng của tôi.
    public class LeaderboardViewModel : ViewModelBase
    {
        private readonly UserEntity _user;
        private readonly ILeaderboardService _lb;
        private readonly ISoloGameService _solo;

        private List<SoloGameSetDto> _allSets = new();
        private bool _suppressReload;
        private int _loadVersion;

        public ObservableCollection<SetBoardRow> SetRows { get; } = new();
        public ObservableCollection<ExpBoardRow> ExpRows { get; } = new();

        public IReadOnlyList<LbModeOption> ModeOptions { get; } = new List<LbModeOption>
        {
            new("Mọi chế độ", null),
            new("Solo", "Solo"),
            new("Multiplayer", "Multiplayer"),
        };

        public IReadOnlyList<LbTimeOption> TimeOptions { get; } = new List<LbTimeOption>
        {
            new("Mọi thời gian", null),
            new("7 ngày qua", 7),
            new("30 ngày qua", 30),
        };

        public ObservableCollection<LbSetOption> SetOptions { get; } = new();

        // =========================================================
        // TAB
        // =========================================================
        private bool _isExpTab = true;
        public bool IsExpTab
        {
            get => _isExpTab;
            set { if (value) SetTab(true); }
        }

        public bool IsSetTab
        {
            get => !_isExpTab;
            set { if (value) SetTab(false); }
        }

        private void SetTab(bool isExp)
        {
            if (_isExpTab == isExp) return;
            _isExpTab = isExp;
            OnPropertyChanged(nameof(IsExpTab));
            OnPropertyChanged(nameof(IsSetTab));
            _ = LoadAsync();
        }

        // =========================================================
        // CHIP ĐỘ KHÓ
        // =========================================================
        private string _difficulty = ""; // "" | "Easy" | "Medium" | "Hard"

        public bool IsDiffAll
        {
            get => _difficulty == "";
            set { if (value) SetDifficulty(""); }
        }
        public bool IsDiffEasy
        {
            get => _difficulty == "Easy";
            set { if (value) SetDifficulty("Easy"); }
        }
        public bool IsDiffMedium
        {
            get => _difficulty == "Medium";
            set { if (value) SetDifficulty("Medium"); }
        }
        public bool IsDiffHard
        {
            get => _difficulty == "Hard";
            set { if (value) SetDifficulty("Hard"); }
        }

        private void SetDifficulty(string value)
        {
            if (_difficulty == value) return;
            _difficulty = value;

            OnPropertyChanged(nameof(IsDiffAll));
            OnPropertyChanged(nameof(IsDiffEasy));
            OnPropertyChanged(nameof(IsDiffMedium));
            OnPropertyChanged(nameof(IsDiffHard));
            OnPropertyChanged(nameof(HasSetFilter));

            RebuildSetOptions();
            _ = LoadAsync();
        }

        // Chỉ hiện ComboBox chọn bộ đề khi đã chọn một mức độ khó cụ thể.
        public bool HasSetFilter => _difficulty != "";

        // =========================================================
        // BỘ LỌC
        // =========================================================
        private LbModeOption _selectedMode;
        public LbModeOption SelectedMode
        {
            get => _selectedMode;
            set
            {
                if (value == null || ReferenceEquals(_selectedMode, value)) return;
                _selectedMode = value;
                OnPropertyChanged();
                if (!_suppressReload) _ = LoadAsync();
            }
        }

        private LbTimeOption _selectedTime;
        public LbTimeOption SelectedTime
        {
            get => _selectedTime;
            set
            {
                if (value == null || ReferenceEquals(_selectedTime, value)) return;
                _selectedTime = value;
                OnPropertyChanged();
                if (!_suppressReload) _ = LoadAsync();
            }
        }

        private LbSetOption? _selectedSetOption;
        public LbSetOption? SelectedSetOption
        {
            get => _selectedSetOption;
            set
            {
                if (ReferenceEquals(_selectedSetOption, value)) return;
                _selectedSetOption = value;
                OnPropertyChanged();
                if (!_suppressReload) _ = LoadAsync();
            }
        }

        // =========================================================
        // TRẠNG THÁI
        // =========================================================
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

        private string _myRankText = "";
        public string MyRankText
        {
            get => _myRankText;
            private set
            {
                _myRankText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasMyRank));
            }
        }

        public bool HasMyRank => !string.IsNullOrEmpty(_myRankText);

        // =========================================================
        // COMMANDS / EVENTS
        // =========================================================
        public ICommand RefreshCommand { get; }
        public ICommand BackCommand { get; }

        public event Action? BackRequested;

        // =========================================================
        // CONSTRUCTOR
        // =========================================================
        public LeaderboardViewModel(UserEntity user)
        {
            _user = user;
            _lb = ClientServiceFactory.GetLeaderboardService();
            _solo = ClientServiceFactory.GetSoloGameService();

            _selectedMode = ModeOptions[0];
            _selectedTime = TimeOptions[0];

            RefreshCommand = new RelayCommand<object>(_ => _ = LoadAsync());
            BackCommand = new RelayCommand<object>(_ => BackRequested?.Invoke());

            _ = InitAsync();
        }

        // Nạp danh sách bộ đề 1 lần rồi load dữ liệu bảng.
        private async Task InitAsync()
        {
            try
            {
                var sets = await Task.Run(() => _solo.GetAvailableSets());
                _allSets = sets ?? new List<SoloGameSetDto>();
            }
            catch (Exception ex) { ErrorMessage = ApiErrorText.From(ex); }

            RebuildSetOptions();
            await LoadAsync();
        }

        // Xây lại danh sách bộ đề theo _difficulty. Không trigger LoadAsync trong lúc build.
        private void RebuildSetOptions()
        {
            _suppressReload = true;
            try
            {
                SetOptions.Clear();
                SetOptions.Add(new LbSetOption("Tất cả bộ đề trong mức", null));

                if (_difficulty != "")
                {
                    foreach (var s in _allSets.Where(s =>
                        string.Equals(s.Difficulty, _difficulty, StringComparison.OrdinalIgnoreCase)))
                    {
                        SetOptions.Add(new LbSetOption(s.SetName, s.SetId));
                    }
                }

                // Chọn lại mục đầu; gán field trực tiếp để bỏ qua setter.
                _selectedSetOption = SetOptions.FirstOrDefault();
                OnPropertyChanged(nameof(SelectedSetOption));
            }
            finally
            {
                _suppressReload = false;
            }
        }

        // =========================================================
        // LOAD
        // Dùng _loadVersion để tránh kết quả cũ ghi đè khi user bấm liên tiếp.
        // KHÔNG dùng "if (IsLoading) return;" — vì sẽ bỏ mất lần lọc mới.
        // =========================================================
        private async Task LoadAsync()
        {
            var version = ++_loadVersion;
            IsLoading = true;
            ErrorMessage = "";

            try
            {
                if (IsExpTab)
                {
                    var list = await Task.Run(() => _lb.GetByExp(100));
                    var me = await Task.Run(() => _lb.GetMyExpRank(_user.Id));

                    if (version != _loadVersion) return;

                    ExpRows.Clear();
                    foreach (var e in list)
                        ExpRows.Add(new ExpBoardRow(e, _user.Id));

                    MyRankText = me != null
                        ? $"Hạng của bạn: #{me.Rank} · {me.LevelBadge} · {me.TotalExp:N0} EXP"
                        : "Bạn chưa có EXP — hãy chơi ván đầu tiên!";
                }
                else
                {
                    var q = new LeaderboardQuery
                    {
                        Mode = SelectedMode?.Value,
                        Difficulty = _difficulty == "" ? null : _difficulty,
                        SetId = SelectedSetOption?.SetId,
                        SinceUtc = SelectedTime?.Days is int d
                            ? DateTime.UtcNow.AddDays(-d)
                            : (DateTime?)null,
                        TopN = 100,
                    };

                    var list = await Task.Run(() => _lb.GetBySet(q));
                    var me = await Task.Run(() => _lb.GetMySetRank(_user.Id, q));

                    if (version != _loadVersion) return;

                    SetRows.Clear();
                    foreach (var e in list)
                        SetRows.Add(new SetBoardRow(e, _user.Id));

                    MyRankText = me != null
                        ? $"Hạng của bạn: #{me.Rank} · {me.OptimalPercent:0.0}% · {me.TimeText}"
                        : "Bạn chưa có thứ hạng trong phạm vi này.";
                }
            }
            catch (Exception ex)
            {
                if (version == _loadVersion) ErrorMessage = ApiErrorText.From(ex);
            }
            finally
            {
                if (version == _loadVersion) IsLoading = false;
            }
        }
    }

    // ---- Option records cho ComboBox ----
    public sealed record LbModeOption(string Label, string? Value);
    public sealed record LbTimeOption(string Label, int? Days);
    public sealed record LbSetOption(string Label, int? SetId);
}