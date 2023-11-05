using Tavis.Models;

namespace TavisApi.Services;

public interface IBcmService
{
  List<Player> GetPlayers();
  DateTime? GetContestStartDate();
  int? CalcBcmValue(double? ratio, double? estimate);
  List<string> GetAlphabetChallengeProgress(long playerId);
  List<Game> GetOddJobChallengeProgress(long playerId);
}
