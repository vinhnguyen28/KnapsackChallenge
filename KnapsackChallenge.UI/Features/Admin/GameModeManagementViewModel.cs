using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using KnapsackChallenge.Core.Factories;
using KnapsackChallenge.Core.Services.Admin;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Shared;

namespace KnapsackChallenge.UI.Features.Admin
{
    // Row VM: bind trực tiếp trên card, có Original* để so sánh khi Save.
    public class GameModeRowViewModel : ViewModelBase
    {
        public string ModeKey { get; }
        public string DisplayName { get; }

        // Giá trị gốc (để so sánh khi Save & confirm khi tắt).
        public bool OriginalIsEnabled { get; private set; }

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        private string _timeLimitText = "0";
        public string TimeLimitText
        {
            get => _timeLimitText;
            set { _timeLimitText = value; OnPropertyChanged(); }
        }

        private string _maxPlayersText = "";
        public string MaxPlayersText
        {
            get => _maxPlayersText;
            set { _maxPlayersText = value; OnPropertyChanged(); }
        }

        private string _updatedInfoText = "";
        public string UpdatedInfoText
        {
            get => _updatedInfoText;
            private set { _updatedInfoText = value; OnPropertyChanged(); }
        }

        // MaxPlayers chỉ áp dụng cho Multiplayer -> ẩn ô nhập khi Solo.
        public bool IsMultiplayer =>
            string.Equals(ModeKey, "Multiplayer", StringComparison.OrdinalIgnoreCase);

        // Nhãn hiển thị cho "ModeKey badge" (mono).
        public string ModeKeyBadge => ModeKey.ToUpperInvariant();

        public GameModeRowViewModel(GameModeEntity e)
        {
            ModeKey = e.ModeKey;
            DisplayName = e.DisplayName;
            ApplyFromEntity(e);
        }

        public void ApplyFromEntity(GameModeEntity e)
        {
            OriginalIsEnabled = e.IsEnabled;
            IsEnabled = e.IsEnabled;
            TimeLimitText = e.TimeLimitSeconds.ToString();
            MaxPlayersText = e.MaxPlayers?.ToString() ?? "";
            UpdatedInfoText = $"Cập nhật cuối: {e.UpdatedAt.ToLocalTime():dd/MM/yyyy HH:mm}" +
                              (string.IsNullOrEmpty(e.UpdatedBy) ? "" : $" bởi {e.UpdatedBy}");
        }
    }

    public class GameModeManagementViewModel : ViewModelBase, IPageLifecycle
    {
        private readonly IGameModeService _service;
        private readonly IDialogService _dialog;

        public ObservableCollection<GameModeRowViewModel> Modes { get; } = new();

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

        private string _infoMessage = "";
        public string InfoMessage
        {
            get => _infoMessage;
            set { _infoMessage = value; OnPropertyChanged(); }
        }

        public ICommand RefreshCommand { get; }
        public ICommand SaveCommand { get; }

        public GameModeManagementViewModel()
        {
            _service = ServiceFactory.GetGameModeService();
            _dialog = new WpfDialogService();

            RefreshCommand = new RelayCommand<object>(_ => _ = LoadAsync());
            SaveCommand = new RelayCommand<GameModeRowViewModel>(row => _ = SaveAsync(row));

            _ = LoadAsync();
        }

        public void OnNavigatedFrom() { /* không có timer -> no-op */ }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                var list = await System.Threading.Tasks.Task.Run(() => _service.GetAll());

                Modes.Clear();
                foreach (var e in list)
                    Modes.Add(new GameModeRowViewModel(e));

                ErrorMessage = "";
                InfoMessage = "";
            }
            catch (SqlException) { ErrorMessage = "Không kết nối được cơ sở dữ liệu!"; }
            catch (InvalidOperationException ex) { ErrorMessage = ex.Message; }
            finally { IsLoading = false; }
        }

        private async System.Threading.Tasks.Task SaveAsync(GameModeRowViewModel? row)
        {
            if (row == null) return;

            ErrorMessage = "";
            InfoMessage = "";

            // Confirm khi TẮT một chế độ đang bật.
            if (row.OriginalIsEnabled && !row.IsEnabled)
            {
                if (!_dialog.Confirm(
                        $"Tắt chế độ \"{row.DisplayName}\"?\nNgười chơi sẽ không thể bắt đầu ván mới ở chế độ này.",
                        "Xác nhận tắt chế độ"))
                    return;
            }

            // Parse thời gian giới hạn.
            if (!int.TryParse(row.TimeLimitText, out int timeLimit) || timeLimit < 0)
            {
                ErrorMessage = "Giới hạn thời gian phải là số nguyên không âm!";
                return;
            }

            // Parse MaxPlayers (chỉ Multiplayer; trống = null).
            int? maxPlayers = null;
            if (row.IsMultiplayer && !string.IsNullOrWhiteSpace(row.MaxPlayersText))
            {
                if (!int.TryParse(row.MaxPlayersText, out int mp))
                {
                    ErrorMessage = "Số người tối đa phải là số nguyên!";
                    return;
                }
                maxPlayers = mp;
            }

            try
            {
                var (ok, message) = await System.Threading.Tasks.Task.Run(() =>
                {
                    // Lấy UserEntity của admin hiện tại: dùng qua AuthService? Không, ta có
                    // sẵn _service.Update cần UserEntity. Ở đây ta chỉ có username khi login,
                    // nhưng MainAdminViewModel đã giữ _adminUser; đơn giản là ta tra cứu lại.
                    // Tránh phụ thuộc: dùng instance UserEntity tối thiểu chỉ với Username.
                    var adminStub = new UserEntity { Username = _currentAdminUsername };
                    return _service.Update(row.ModeKey, row.IsEnabled, timeLimit, maxPlayers, adminStub);
                });

                if (ok)
                {
                    InfoMessage = message;
                    // Reload để cập nhật UpdatedAt / UpdatedBy mới.
                    await LoadAsync();
                }
                else
                {
                    ErrorMessage = message;
                }
            }
            catch (SqlException) { ErrorMessage = "Lỗi cơ sở dữ liệu khi lưu cấu hình!"; }
        }

        // Tên admin hiện tại (gán từ MainAdminViewModel khi khởi tạo qua property).
        private string _currentAdminUsername = "";
        public void SetCurrentAdmin(string username) => _currentAdminUsername = username ?? "";
    }
}