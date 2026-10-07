using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Factories;
using KnapsackChallenge.Core.Services.Admin;
using KnapsackChallenge.Core.Services.Player;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Shared;

namespace KnapsackChallenge.UI.Features.Player
{
    // Trang chủ người chơi: 2 nút lớn + cột stats + cột top + 2 shortcut.
    public class HomePageViewModel : ViewModelBase
    {
        private const bool MultiplayerImplemented = false;
        private const int LeaderboardLoadCount = 100;

        private readonly UserEntity _user;
        private readonly IGameModeService _gameModeService;
        private readonly ISoloGameService _soloGameService;

        public string WelcomeText => $"Chào {_user.Username ?? ""}, sẵn sàng chưa?";

        // ---- Placeholder stats ----
        public string BestScoreText => "—";
        public string CurrentRankText => "Chưa xếp hạng";
        public string GamesPlayedText => "0";

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
        public string MultiplayerSubtitle => MultiplayerEnabled ? "Sắp ra mắt" : "Đang tạm đóng";

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
    }
}