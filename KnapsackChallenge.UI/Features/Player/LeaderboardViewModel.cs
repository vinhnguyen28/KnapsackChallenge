using System;
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
    // Trang Xếp hạng tách từ tab cũ trong SoloGameView.
    // Nguồn dữ liệu vẫn là ISoloGameService.GetLeaderboard(setId?, topN).
    public class LeaderboardViewModel : ViewModelBase
    {
        // Ở đây là màn chuyên dụng, có thể nạp nhiều hơn HomePage (chỉ nạp 100 cho mini-board).
        private const int TopCount = 100;

        private readonly ISoloGameService _soloService;

        public ObservableCollection<LeaderboardEntryDto> Leaderboard { get; } = new();
        public ObservableCollection<LeaderboardFilterOption> LeaderboardFilters { get; } = new();

        private LeaderboardFilterOption? _selectedFilter;
        public LeaderboardFilterOption? SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (ReferenceEquals(_selectedFilter, value)) return;
                _selectedFilter = value;
                OnPropertyChanged();
                _ = LoadAsync();
            }
        }

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

        public ICommand RefreshCommand { get; }

        // user hiện không dùng trong luồng tải BXH, nhưng giữ tham số để sau này
        // highlight dòng của chính mình mà không phải đổi constructor.
        public LeaderboardViewModel(UserEntity user)
        {
            _ = user;
            _soloService = ServiceFactory.GetSoloGameService();

            RefreshCommand = new RelayCommand<object>(_ => _ = LoadAsync());

            _ = LoadFiltersThenDataAsync();
        }

        private async System.Threading.Tasks.Task LoadFiltersThenDataAsync()
        {
            // Nạp danh sách bộ đề cho filter (dùng SoloGameService.GetAvailableSets).
            try
            {
                var sets = await System.Threading.Tasks.Task.Run(() => _soloService.GetAvailableSets());

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
                _selectedFilter = LeaderboardFilters.FirstOrDefault();
                OnPropertyChanged(nameof(SelectedFilter));
            }
            catch (SqlException) { ErrorMessage = "Không kết nối được cơ sở dữ liệu!"; }
            catch (InvalidOperationException ex) { ErrorMessage = ex.Message; }

            await LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                int? setId = _selectedFilter?.SetId;
                var list = await System.Threading.Tasks.Task.Run(
                    () => _soloService.GetLeaderboard(setId, TopCount));

                Leaderboard.Clear();
                foreach (var e in list) Leaderboard.Add(e);

                ErrorMessage = "";
            }
            catch (SqlException) { ErrorMessage = "Không kết nối được cơ sở dữ liệu!"; }
            finally { IsLoading = false; }
        }
    }
}