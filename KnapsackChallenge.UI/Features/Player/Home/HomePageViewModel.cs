using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Factories;
using KnapsackChallenge.Core.Services.Admin;
using KnapsackChallenge.Core.Services.Player;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Shared;
using Microsoft.Data.SqlClient;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace KnapsackChallenge.UI.Features.Player
{
    // Trang chủ người chơi: 2 nút lớn + cột stats + cột top + 2 shortcut.
    public class HomePageViewModel : ViewModelBase
    {

        private const bool MultiplayerImplemented = true;
        private const int LeaderboardLoadCount = 100;

        private readonly UserEntity _user;
        private readonly IGameModeService _gameModeService;
        private readonly ISoloGameService _soloGameService;
        private readonly IRankService _rankService;

        public string WelcomeText => $"Chào {_user.Username ?? ""}, sẵn sàng chưa?";

        // ---- Placeholder stats ----
        // ---- Stats thực từ DB ----
        private string _bestScoreText = "—";
        public string BestScoreText
        {
            get => _bestScoreText;
            private set { _bestScoreText = value; OnPropertyChanged(); }
        }

        private string _currentRankText = "Lv 1 · Đồng";
        public string CurrentRankText
        {
            get => _currentRankText;
            private set { _currentRankText = value; OnPropertyChanged(); }
        }

        private string _gamesPlayedText = "0";
        public string GamesPlayedText
        {
            get => _gamesPlayedText;
            private set { _gamesPlayedText = value; OnPropertyChanged(); }
        }

        // ---- v8: EXP / Level ----
        private long _totalExp;
        public long TotalExp
        {
            get => _totalExp;
            private set { _totalExp = value; OnPropertyChanged(); }
        }

        private int _level = 1;
        public int Level
        {
            get => _level;
            private set { _level = value; OnPropertyChanged(); OnPropertyChanged(nameof(LevelBadge)); }
        }

        private string _rankTitle = "Đồng";
        public string RankTitle
        {
            get => _rankTitle;
            private set { _rankTitle = value; OnPropertyChanged(); OnPropertyChanged(nameof(LevelBadge)); }
        }

        private double _expProgressPercent;
        public double ExpProgressPercent
        {
            get => _expProgressPercent;
            private set { _expProgressPercent = value; OnPropertyChanged(); }
        }

        private string _expProgressText = "0 / 100 EXP";
        public string ExpProgressText
        {
            get => _expProgressText;
            private set { _expProgressText = value; OnPropertyChanged(); }
        }

        public string LevelBadge => $"Lv {Level} · {RankTitle}";

        // ---- Top người chơi ----
        public ObservableCollection<LeaderboardEntryDto> TopPlayers { get; } = new();

        private bool _isLoadingTopPlayers = true;
        public bool IsLoadingTopPlayers
        {
            get => _isLoadingTopPlayers;
            private set { _isLoadingTopPlayers = value; OnPropertyChanged(); }
        }

        // ---- Trạng thái chế độ ----
        private bool _soloEnabled = true;
        public bool SoloEnabled
        {
            get => _soloEnabled;
            private set
            {
                _soloEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanPlaySolo));
                OnPropertyChanged(nameof(SoloStatusText));
            }
        }

        private bool _multiplayerEnabled;
        public bool MultiplayerEnabled
        {
            get => _multiplayerEnabled;
            private set
            {
                _multiplayerEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanPlayMultiplayer));
                OnPropertyChanged(nameof(MultiplayerSubtitle));
            }
        }

        public bool CanPlaySolo => SoloEnabled;
        public bool CanPlayMultiplayer => MultiplayerEnabled && MultiplayerImplemented;
        public string SoloStatusText => SoloEnabled ? "Đang hoạt động" : "Tạm đóng";
        public string MultiplayerSubtitle => MultiplayerEnabled ? "Đang hoạt động" : "Đang tạm đóng";

        // ---- Commands ----
        public ICommand PlaySoloCommand { get; }
        public ICommand PlayMultiplayerCommand { get; }
        public ICommand OpenLeaderboardCommand { get; }
        public ICommand OpenHistoryCommand { get; }

        // ---- Events ----
        public event Action? PlaySoloRequested;
        public event Action? PlayMultiplayerRequested;
        public event Action? OpenLeaderboardRequested;
        public event Action? OpenHistoryRequested;

        public HomePageViewModel(UserEntity user)
        {
            _user = user;
            _gameModeService = ServiceFactory.GetGameModeService();
            _soloGameService = ServiceFactory.GetSoloGameService();
            _rankService = ServiceFactory.GetRankService();

            PlaySoloCommand = new RelayCommand<object>(
                _ => PlaySoloRequested?.Invoke(),
                _ => CanPlaySolo);

            PlayMultiplayerCommand = new RelayCommand<object>(
                _ => PlayMultiplayerRequested?.Invoke(),
                _ => CanPlayMultiplayer);

            OpenLeaderboardCommand = new RelayCommand<object>(_ => OpenLeaderboardRequested?.Invoke());
            OpenHistoryCommand = new RelayCommand<object>(_ => OpenHistoryRequested?.Invoke());

            _ = LoadModeStatusAsync();
            _ = LoadTopPlayersAsync();
            _ = LoadMyStatsAsync();
        }

        private async System.Threading.Tasks.Task LoadModeStatusAsync()
        {
            try
            {
                var modes = await System.Threading.Tasks.Task.Run(() => _gameModeService.GetAll());

                var solo = modes.FirstOrDefault(m =>
                    string.Equals(m.ModeKey, "Solo", StringComparison.OrdinalIgnoreCase));
                var multi = modes.FirstOrDefault(m =>
                    string.Equals(m.ModeKey, "Multiplayer", StringComparison.OrdinalIgnoreCase));

                SoloEnabled = solo?.IsEnabled ?? true;
                MultiplayerEnabled = multi?.IsEnabled ?? false;
            }
            catch (SqlException) { }
            catch (InvalidOperationException) { }
        }

        private async System.Threading.Tasks.Task LoadTopPlayersAsync()
        {
            IsLoadingTopPlayers = true;
            try
            {
                var list = await System.Threading.Tasks.Task.Run(
                    () => _soloGameService.GetLeaderboard(null, LeaderboardLoadCount));

                TopPlayers.Clear();
                foreach (var e in list) TopPlayers.Add(e);
            }
            catch (SqlException) { }
            catch (InvalidOperationException) { }
            finally { IsLoadingTopPlayers = false; }
        }

        private async System.Threading.Tasks.Task LoadMyStatsAsync()
        {
            try
            {
                var rank = await System.Threading.Tasks.Task.Run(() => _rankService.GetStatus(_user.Id));

                TotalExp = rank.TotalExp;
                Level = rank.Level;
                RankTitle = rank.RankTitle;
                ExpProgressPercent = rank.ProgressPercent;
                ExpProgressText = rank.ProgressText;
                CurrentRankText = rank.LevelBadge;

                var history = await System.Threading.Tasks.Task.Run(
                    () => _soloGameService.GetHistory(_user.Id, 100));

                GamesPlayedText = history.Count.ToString();
                int best = history
                    .Where(h => h.IsSubmitted)
                    .Select(h => h.TotalScore)
                    .DefaultIfEmpty(0)
                    .Max();
                BestScoreText = best > 0 ? best.ToString() : "—";
            }
            catch (SqlException) { }
            catch (InvalidOperationException) { }
        }
    }
}