using System.Collections.ObjectModel;
using Microsoft.Data.SqlClient;
using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Factories;
using KnapsackChallenge.Core.Services.Player;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Shared;

namespace KnapsackChallenge.UI.Features.Player
{
    // Trang Lịch sử: các ván gần đây của người chơi hiện tại.
    public class HistoryViewModel : ViewModelBase
    {
        private const int HistoryLimit = 100;

        private readonly ISoloGameService _soloService;
        private readonly UserEntity _user;

        public ObservableCollection<GameHistoryDto> History { get; } = new();

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

        public HistoryViewModel(UserEntity user)
        {
            _user = user;
            _soloService = ServiceFactory.GetSoloGameService();
            _ = LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                var list = await System.Threading.Tasks.Task.Run(
                    () => _soloService.GetHistory(_user.Id, HistoryLimit));

                History.Clear();
                foreach (var h in list) History.Add(h);

                ErrorMessage = "";
            }
            catch (SqlException) { ErrorMessage = "Không kết nối được cơ sở dữ liệu!"; }
            finally { IsLoading = false; }
        }
    }
}