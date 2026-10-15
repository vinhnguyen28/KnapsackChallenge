using KnapsackChallenge.Core.Services.Player;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Services;
using KnapsackChallenge.UI.Shared;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace KnapsackChallenge.UI.Features.Player
{
    // Thẻ mức độ trên màn chọn bộ đề.
    public class DifficultyCardViewModel
    {
        public string Difficulty { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Description { get; init; } = "";
        public string Icon { get; init; } = "";
        public List<int> SetIds { get; init; } = new();

        public int SetCount => SetIds.Count;
        public bool CanPlay => SetCount > 0;

        public string CountText => SetCount == 0
            ? "Chưa có bộ đề"
            : $"{SetCount} bộ đề";

        // Thêm mapping Level dùng cho việc đếm vạch đo độ khó.
        public int Level => Difficulty switch { "Easy" => 1, "Medium" => 2, "Hard" => 3, _ => 0 };
    }

    // Màn chọn mức độ trước khi vào chơi.
    public class SetSelectionViewModel : ViewModelBase
    {
        private readonly UserEntity _user;
        private readonly ISoloGameService _soloService;

        public ObservableCollection<DifficultyCardViewModel> Difficulties { get; } = new();

        private bool _isLoading = true;
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

        public event Action<int>? SetChosen;
        public event Action? BackRequested;

        public ICommand ChooseDifficultyCommand { get; }
        public ICommand BackCommand { get; }

        public SetSelectionViewModel(UserEntity user)
        {
            _user = user;
            _soloService = ClientServiceFactory.GetSoloGameService();

            ChooseDifficultyCommand = new RelayCommand<DifficultyCardViewModel>(
                ChooseDifficulty,
                card => card?.CanPlay == true);

            BackCommand = new RelayCommand<object>(_ => BackRequested?.Invoke());

            _ = LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var sets = await System.Threading.Tasks.Task.Run(() => _soloService.GetAvailableSets());

                var easy = sets.Where(s => Eq(s.Difficulty, "Easy")).Select(s => s.SetId).ToList();
                var medium = sets.Where(s => Eq(s.Difficulty, "Medium")).Select(s => s.SetId).ToList();
                var hard = sets.Where(s => Eq(s.Difficulty, "Hard")).Select(s => s.SetId).ToList();

                Difficulties.Clear();
                Difficulties.Add(new DifficultyCardViewModel
                {
                    Difficulty = "Easy",
                    DisplayName = "Dễ",
                    Description = "Làm quen với bài toán",
                    Icon = "🌱",
                    SetIds = easy,
                });
                Difficulties.Add(new DifficultyCardViewModel
                {
                    Difficulty = "Medium",
                    DisplayName = "Bình thường",
                    Description = "Thử thách vừa phải",
                    Icon = "⚖️",
                    SetIds = medium,
                });
                Difficulties.Add(new DifficultyCardViewModel
                {
                    Difficulty = "Hard",
                    DisplayName = "Khó",
                    Description = "Chinh phục đỉnh cao",
                    Icon = "🔥",
                    SetIds = hard,
                });

                ErrorMessage = "";
            }
            catch (Exception ex) { ErrorMessage = ApiErrorText.From(ex); }
            finally { IsLoading = false; }
        }

        private void ChooseDifficulty(DifficultyCardViewModel? card)
        {
            if (card == null || card.SetIds.Count == 0) return;

            var rnd = Random.Shared;
            int setId = card.SetIds[rnd.Next(card.SetIds.Count)];
            SetChosen?.Invoke(setId);
        }

        private static bool Eq(string? a, string b)
            => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}