using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Services.Player;
using Xunit;

namespace KnapsackChallenge.Tests
{
    // Normalize là logic thuần, không cần mock repo.
    // Nhờ [InternalsVisibleTo] trong Core mà test gọi được method internal.
    public class LeaderboardServiceNormalizeTests
    {
        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("   ", null)]
        [InlineData("abc", null)]
        [InlineData("solo", "Solo")]
        [InlineData("MULTIPLAYER", "Multiplayer")]
        [InlineData("  Solo  ", "Solo")]
        public void Normalize_Mode(string? input, string? expected)
        {
            var q = new LeaderboardQuery { Mode = input };
            Assert.Equal(expected, LeaderboardService.Normalize(q).Mode);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("abc", null)]
        [InlineData("hard", "Hard")]
        [InlineData("EASY", "Easy")]
        [InlineData("  Medium  ", "Medium")]
        public void Normalize_Difficulty(string? input, string? expected)
        {
            var q = new LeaderboardQuery { Difficulty = input };
            Assert.Equal(expected, LeaderboardService.Normalize(q).Difficulty);
        }

        [Theory]
        [InlineData(-5, 1)]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(50, 50)]
        [InlineData(200, 200)]
        [InlineData(201, 200)]
        [InlineData(9999, 200)]
        public void Normalize_TopN_Clamp(int input, int expected)
        {
            var q = new LeaderboardQuery { TopN = input };
            Assert.Equal(expected, LeaderboardService.Normalize(q).TopN);
        }

        [Fact]
        public void Normalize_SetId_NonPositive_Becomes_Null()
        {
            Assert.Null(LeaderboardService.Normalize(new LeaderboardQuery { SetId = null }).SetId);
            Assert.Null(LeaderboardService.Normalize(new LeaderboardQuery { SetId = 0 }).SetId);
            Assert.Null(LeaderboardService.Normalize(new LeaderboardQuery { SetId = -3 }).SetId);
            Assert.Equal(5, LeaderboardService.Normalize(new LeaderboardQuery { SetId = 5 }).SetId);
        }

        [Fact]
        public void Normalize_Does_Not_Mutate_Original()
        {
            var original = new LeaderboardQuery { Mode = "abc", TopN = 9999, SetId = 0 };
            _ = LeaderboardService.Normalize(original);

            Assert.Equal("abc", original.Mode);
            Assert.Equal(9999, original.TopN);
            Assert.Equal(0, original.SetId);
        }

        [Fact]
        public void Normalize_Null_Input_Returns_Defaults()
        {
            var n = LeaderboardService.Normalize(null);
            Assert.Null(n.Mode);
            Assert.Null(n.Difficulty);
            Assert.Null(n.SetId);
            Assert.Null(n.SinceUtc);
            Assert.Equal(100, n.TopN); // mặc định của LeaderboardQuery
        }
    }
}