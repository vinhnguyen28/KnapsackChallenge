using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Shared;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace KnapsackChallenge.UI.Features.Player
{
    // Wrapper 1 dòng bảng xếp hạng cuối ván.
    public class MultiplayerResultEntryRow
    {
        private readonly int _myUserId;
        public FinalRankingEntryDto Data { get; }

        public MultiplayerResultEntryRow(FinalRankingEntryDto d, int myUserId)
        {
            Data = d;
            _myUserId = myUserId;
        }

        public int Rank => Data.Rank;
        public int UserId => Data.UserId;
        public string Username => Data.Username;
        public int TotalScore => Data.TotalScore;
        public int TotalWeight => Data.TotalWeight;
        public int OptimalValue => Data.OptimalValue;
        public double OptimalPercent => Data.OptimalPercent;
        public int TimeSpentSeconds => Data.TimeSpentSeconds;
        public bool IsSubmitted => Data.IsSubmitted;
        public bool IsKicked => Data.IsKicked;
        public bool IsBanned => Data.IsBanned;
        public bool IsLeft => Data.IsLeft;
        public bool IsMe => Data.UserId == _myUserId;

        public string StatusText => IsKicked ? "Bị mời"
            : IsBanned ? "Bị khóa"
            : IsLeft ? "Đã rời"
            : IsSubmitted ? "Đã nộp"
            : "Không nộp";

        public string TimeText => TimeSpentSeconds <= 0 ? "—" : $"{TimeSpentSeconds}s";
        public string OptimalPercentText => $"{OptimalPercent:0.0}%";
    }

    // Màn Bảng xếp hạng cuối ván. Không subscribe SignalR — chỉ hiển thị dữ liệu
    // đã nhận từ GameEnded. Trong grace 60s server vẫn giữ room; BackToLobby đi
    // qua Lobby mới tinh để refresh danh sách phòng.
    public class MultiplayerResultViewModel : ViewModelBase, IPageLifecycle
    {
        private readonly UserEntity _user;
        private readonly FinalRankingDto _ranking;

        public ObservableCollection<MultiplayerResultEntryRow> Entries { get; } = new();

        public event Action? BackToLobbyRequested;

        public ICommand BackToLobbyCommand { get; }

        public MultiplayerResultViewModel(UserEntity user, FinalRankingDto ranking)
        {
            _user = user;
            _ranking = ranking;

            foreach (var e in ranking.Entries)
                Entries.Add(new MultiplayerResultEntryRow(e, user.Id));

            BackToLobbyCommand = new RelayCommand<object>(_ => BackToLobbyRequested?.Invoke());
        }

        public string RoomCode => _ranking.RoomCode;
        public string SetName => _ranking.SetName;
        public int OptimalValue => _ranking.OptimalValue;
        public int TotalPlayers => _ranking.Entries.Count;

        public string FinishedAtText => _ranking.FinishedAtUtc == default
            ? "—"
            : _ranking.FinishedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

        // "Bạn xếp hạng #N" — tìm entry của chính mình.
        public string MyRankText
        {
            get
            {
                var me = _ranking.Entries.FirstOrDefault(e => e.UserId == _user.Id);
                return me == null ? "—" : $"#{me.Rank} / {_ranking.Entries.Count}";
            }
        }

        public void OnNavigatedFrom() { }
    }
}