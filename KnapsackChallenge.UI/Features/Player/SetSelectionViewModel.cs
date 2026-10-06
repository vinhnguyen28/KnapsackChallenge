using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Factories;
using KnapsackChallenge.Core.Services.Player;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Shared;

namespace KnapsackChallenge.UI.Features.Player
{
    // Thẻ mức độ trên màn chọn bộ đề.
    // - Difficulty: giá trị kỹ thuật khớp DB ('Easy' / 'Medium' / 'Hard').
    // - SetIds: danh sách setId có cùng độ khó -> dùng để random.
    public class DifficultyCardViewModel
    {
        public string Difficulty { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Description { get; init; } = "";
        public string Icon { get; init; } = "";
        public List<int> SetIds { get; init; } = new();

        public int SetCount => SetIds.Count;
        public bool CanPlay => SetCount > 0;

        // Chuỗi phụ trên thẻ, khác nhau khi rỗng vs có dữ liệu.
        public string CountText => SetCount == 0
            ? "Chưa có bộ đề"
            : $"{SetCount} bộ đề";
    }

    // Màn chọn mức độ trước khi vào chơi. Khi user chọn 1 mức, VM random 1 setId
    // trong mức đó và phát event SetChosen(setId) cho vỏ xử lý điều hướng.
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

        // Phát khi user chọn xong 1 mức và ta đã random ra setId.
        public event Action<int>? SetChosen;
        // Phát khi user bấm "Quay lại".
        public event Action? BackRequested;

        public ICommand ChooseDifficultyCommand { get; }
        public ICommand BackCommand { get; }

        public SetSelectionViewModel(UserEntity user)
        {
            _user = user;
            _soloService = ServiceFactory.GetSoloGameService();

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

                // Nhóm theo độ khó. Bộ đề nào difficulty rỗng coi như "Easy" để tránh mất dữ liệu.
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
            catch (SqlException) { ErrorMessage = "Không kết nối được cơ sở dữ liệu!"; }
            catch (InvalidOperationException ex) { ErrorMessage = ex.Message; }
            finally { IsLoading = false; }
        }

        private void ChooseDifficulty(DifficultyCardViewModel? card)
        {
            if (card == null || card.SetIds.Count == 0) return;

            // Random 1 bộ đề trong mức đã chọn.
            var rnd = Random.Shared;
            int setId = card.SetIds[rnd.Next(card.SetIds.Count)];
            SetChosen?.Invoke(setId);
        }

        private static bool Eq(string? a, string b)
            => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}