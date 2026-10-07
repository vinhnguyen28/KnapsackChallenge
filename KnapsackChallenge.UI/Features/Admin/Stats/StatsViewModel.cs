using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Factories;
using KnapsackChallenge.Core.Services.Admin;
using KnapsackChallenge.UI.Shared;

namespace KnapsackChallenge.UI.Features.Admin
{
    // Dòng biểu đồ cột: nhãn + giá trị (%) + chiều cao pixel đã scale sẵn.
    // Đặt tại đây để XAML bind trực tiếp Height (không cần converter).
    public class ChartBarItem
    {
        public string Label { get; set; } = "";
        public double Value { get; set; }       // % tối ưu trung bình (0..100+)
        public double BarHeight { get; set; }   // pixel (0..150)
        public string ValueText => Value.ToString("0.0") + "%";
    }

    // Lựa chọn trong ComboBox bộ lọc.
    public class StatsFilterOption
    {
        public string? Value { get; init; }         // null = tất cả
        public string DisplayText { get; init; } = "";
    }

    public class StatsViewModel : ViewModelBase, IPageLifecycle
    {
        // Chiều cao tối đa của cột biểu đồ (pixel).
        private const double MaxBarHeight = 150.0;

        private readonly IAdminStatsService _service;

        public ObservableCollection<SetStatsDto> SetStats { get; } = new();
        public ObservableCollection<TopPlayerDto> TopPlayers { get; } = new();
        public ObservableCollection<ChartBarItem> ChartBars { get; } = new();

        public ObservableCollection<StatsFilterOption> ModeOptions { get; } = new()
        {
            new() { Value = null,          DisplayText = "Tất cả chế độ" },
            new() { Value = "Solo",        DisplayText = "Solo"           },
            new() { Value = "Multiplayer", DisplayText = "Multiplayer"    },
        };

        public ObservableCollection<StatsFilterOption> TimeOptions { get; } = new()
        {
            new() { Value = "7",   DisplayText = "7 ngày gần đây"  },
            new() { Value = "30",  DisplayText = "30 ngày gần đây" },
            new() { Value = "all", DisplayText = "Tất cả thời gian" },
        };

        private StatsFilterOption _selectedMode;
        public StatsFilterOption SelectedMode
        {
            get => _selectedMode;
            set
            {
                if (value == null || ReferenceEquals(_selectedMode, value)) return;
                _selectedMode = value;
                OnPropertyChanged();
                _ = LoadAsync();
            }
        }

        private StatsFilterOption _selectedTime;
        public StatsFilterOption SelectedTime
        {
            get => _selectedTime;
            set
            {
                if (value == null || ReferenceEquals(_selectedTime, value)) return;
                _selectedTime = value;
                OnPropertyChanged();
                _ = LoadAsync();
            }
        }

        // Overview stats.
        private int _totalGames;
        public int TotalGames { get => _totalGames; private set { _totalGames = value; OnPropertyChanged(); } }

        private double _optimalRate;
        public double OptimalRate { get => _optimalRate; private set { _optimalRate = value; OnPropertyChanged(); } }

        private double _avgOptimalPercent;
        public double AvgOptimalPercent { get => _avgOptimalPercent; private set { _avgOptimalPercent = value; OnPropertyChanged(); } }

        private int _maxScore;
        public int MaxScore { get => _maxScore; private set { _maxScore = value; OnPropertyChanged(); } }

        private string? _maxScoreHolder;
        public string? MaxScoreHolder { get => _maxScoreHolder; private set { _maxScoreHolder = value; OnPropertyChanged(); } }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set { _isLoading = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEmpty)); }
        }

        private string _errorMessage = "";
        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        private string _infoMessage = "";
        public string InfoMessage
        {
            get => _infoMessage;
            set { _infoMessage = value; OnPropertyChanged(); }
        }

        // Trạng thái trống: chưa có dữ liệu (không hiện bảng + chart).
        public bool IsEmpty => !IsLoading && TotalGames == 0;

        public ICommand RefreshCommand { get; }

        public StatsViewModel()
        {
            _service = ServiceFactory.GetAdminStatsService();

            _selectedMode = ModeOptions[0];
            _selectedTime = TimeOptions[2]; // mặc định "Tất cả thời gian"

            RefreshCommand = new RelayCommand<object>(_ => _ = LoadAsync());

            _ = LoadAsync();
        }

        // Không có timer -> không cần làm gì khi điều hướng. Giữ interface để nhất quán.
        public void OnNavigatedFrom() { }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                var mode = _selectedMode?.Value;
                DateTime? fromDate = ResolveFromDate(_selectedTime?.Value);

                // Chạy song song 3 truy vấn để giảm thời gian chờ.
                var taskOverview = System.Threading.Tasks.Task.Run(
                    () => _service.GetOverview(mode, fromDate));
                var taskSets = System.Threading.Tasks.Task.Run(
                    () => _service.GetSetStats(mode, fromDate));
                var taskTop = System.Threading.Tasks.Task.Run(
                    () => _service.GetTopPlayers(mode, fromDate, 10));

                await System.Threading.Tasks.Task.WhenAll(taskOverview, taskSets, taskTop);

                var overview = taskOverview.Result;
                var sets = taskSets.Result;
                var top = taskTop.Result;

                // Cập nhật overview.
                TotalGames = overview.TotalGames;
                OptimalRate = overview.OptimalRate;
                AvgOptimalPercent = overview.AvgOptimalPercent;
                MaxScore = overview.MaxScore;
                MaxScoreHolder = overview.MaxScoreHolder;
                OnPropertyChanged(nameof(IsEmpty));

                // Bảng theo bộ đề.
                SetStats.Clear();
                foreach (var s in sets) SetStats.Add(s);

                // Top người chơi.
                TopPlayers.Clear();
                foreach (var t in top) TopPlayers.Add(t);

                // Biểu đồ cột: lấy 8 bộ đề có lượt chơi nhiều nhất để không bị chật.
                ChartBars.Clear();
                foreach (var s in sets.Take(8))
                {
                    double barHeight = s.AvgOptimalPercent / 100.0 * MaxBarHeight;
                    if (barHeight < 3 && s.AvgOptimalPercent > 0) barHeight = 3; // tối thiểu 3px cho thấy có dữ liệu
                    ChartBars.Add(new ChartBarItem
                    {
                        Label = s.SetName,
                        Value = s.AvgOptimalPercent,
                        BarHeight = barHeight,
                    });
                }

                InfoMessage = "";
                ErrorMessage = "";
            }
            catch (SqlException) { ErrorMessage = "Không kết nối được cơ sở dữ liệu!"; }
            catch (InvalidOperationException ex) { ErrorMessage = ex.Message; }
            finally { IsLoading = false; }
        }

        private static DateTime? ResolveFromDate(string? timeKey)
        {
            if (string.IsNullOrEmpty(timeKey) || timeKey == "all") return null;
            if (timeKey == "7") return DateTime.UtcNow.AddDays(-7);
            if (timeKey == "30") return DateTime.UtcNow.AddDays(-30);
            return null;
        }
    }
}