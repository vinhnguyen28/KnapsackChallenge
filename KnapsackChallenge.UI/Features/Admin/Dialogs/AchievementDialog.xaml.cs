using System.Globalization;
using System.Windows;
using KnapsackChallenge.Common.DTOs;

namespace KnapsackChallenge.UI.Features.Admin.Dialogs
{
    public partial class AchievementDialog : Window
    {
        public AchievementDialog(PlayerAchievementDto data)
        {
            InitializeComponent();

            UsernameText.Text = $"{data.Username}  •  Role: {data.Role}";

            GeneralInfoText.Text =
                $"Ngày tạo: {data.CreatedAt.ToLocalTime():dd/MM/yyyy HH:mm}    •    " +
                $"Đăng nhập cuối: {(data.LastLoginAt.HasValue ? data.LastLoginAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) : "—")}";

            if (data.IsBanned)
            {
                BanBanner.Visibility = Visibility.Visible;
                BanHeaderText.Text = $"Tài khoản đang bị BAN" +
                    (data.BannedAt.HasValue ? $" (từ {data.BannedAt.Value.ToLocalTime():dd/MM/yyyy})" : "") +
                    (string.IsNullOrEmpty(data.BannedBy) ? "" : $" bởi {data.BannedBy}");
                BanReasonText.Text = "Lý do: " + (string.IsNullOrWhiteSpace(data.BanReason) ? "(không có)" : data.BanReason);
            }

            TotalGamesText.Text = data.TotalGames.ToString();
            SubmittedText.Text = data.SubmittedGames.ToString();
            HighestText.Text = data.HighestScore.ToString();
            AverageText.Text = data.AverageScore.ToString("0.0");
            TotalScoreText.Text = data.TotalScore.ToString();

            GamesGrid.ItemsSource = data.RecentGames;
            if (data.RecentGames.Count == 0)
                EmptyPanel.Visibility = Visibility.Visible;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}