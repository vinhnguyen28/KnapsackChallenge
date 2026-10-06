using KnapsackChallenge.Data.Entities;

namespace KnapsackChallenge.Core.Services.Admin
{
    public interface IGameModeService
    {
        List<GameModeEntity> GetAll();
        GameModeEntity? GetByKey(string modeKey);
        bool IsEnabled(string modeKey);

        // Validate: TimeLimitSeconds 0..3600, MaxPlayers 2..10 (chỉ với Multiplayer).
        // Ghi UpdatedBy/UpdatedAt tự động.
        (bool Success, string Message) Update(string modeKey,
                                              bool isEnabled,
                                              int timeLimitSeconds,
                                              int? maxPlayers,
                                              UserEntity currentAdmin);
    }
}