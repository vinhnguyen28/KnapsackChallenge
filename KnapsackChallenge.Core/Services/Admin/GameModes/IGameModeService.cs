using KnapsackChallenge.Data.Entities;

namespace KnapsackChallenge.Core.Services.Admin
{
    public interface IGameModeService
    {
        List<GameModeEntity> GetAll();
        GameModeEntity? GetByKey(string modeKey);
        bool IsEnabled(string modeKey);

        // Validate:
        //   TimeLimitSeconds   : 0..3600
        //   MaxPlayers         : 2..10 (chỉ Multiplayer)
        //   MaxHearts          : 1..999 (chỉ Solo)
        //   HeartRefillMinutes : 1..1440 (chỉ Solo)
        (bool Success, string Message) Update(string modeKey,
                                              bool isEnabled,
                                              int timeLimitSeconds,
                                              int? maxPlayers,
                                              int? maxHearts,
                                              int? heartRefillMinutes,
                                              UserEntity currentAdmin);
    }
}