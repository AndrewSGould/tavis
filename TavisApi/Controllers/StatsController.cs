namespace TavisApi.Controllers;

using TavisApi.Context;
using TavisApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Tavis.Models;
using System.Data;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using TavisApi.Models;
using System.Numerics;
using Tavis.Extensions;
using TavisApi.ContestRules;

[ApiController]
[Route("[controller]")]
public class StatsController : ControllerBase
{
  private TavisContext _context;
  private readonly IBcmService _bcmService;
  private readonly IStatsService _statsService;

  public StatsController(TavisContext context, IBcmService bcmService, IStatsService statsService)
  {
    _context = context;
    _bcmService = bcmService;
    _statsService = statsService;
  }

  [HttpGet]
  [Route("getBcmLeaderboardList")]
  public IActionResult BcmLeaderboardList()
  {
    var players = _bcmService.GetPlayers();

    foreach (var player in players)
    {
      var bcmStats = _context.BcmStats.FirstOrDefault(x => x.PlayerId == player.Id);
    }

    return Ok(players.OrderBy(x => x.BcmStats?.Rank ?? 999));
  }

  [HttpGet, Authorize(Roles = "Guest")]
  [Route("miscSummary")]
  public IActionResult GetMiscSummary(string player)
  {
    var localuser = _context.Users.FirstOrDefault(x => x.Gamertag == player);
    if (localuser is null) return BadRequest("Player not found with the provided gamertag");

    var bcmPlayer = _context.BcmPlayers.FirstOrDefault(x => x.UserId == localuser.Id);
    if (bcmPlayer is null) return BadRequest("BCM Player not found for the provided user");

    var miscStats = _context.BcmMiscStats.FirstOrDefault(x => x.PlayerId == bcmPlayer.Id);
    if (miscStats is null) return Ok();

    var historicalStats = JsonConvert.DeserializeObject<List<BcmHistoricalStats>>(miscStats.HistoricalStats!);

    return Ok(historicalStats);
  }

  [HttpPost, Authorize(Roles = "Admin, Bcm Admin")]
  [Route("recalcBcmLeaderboard")]
  public async Task<IActionResult> RecalcBcmLeaderboard()
  {
    var players = _bcmService.GetPlayers();

    var leaderboardList = new List<Ranking>();

    foreach (var player in players)
    {
      var playerBcmStats = _context.BcmStats.FirstOrDefault(x => x.PlayerId == player.Id);

      if (playerBcmStats == null)
      {
        playerBcmStats = new BcmStat();
        _context.BcmStats.Add(playerBcmStats);
      }

      playerBcmStats.PlayerId = player.Id;

      var userWithReg = _context.Users.Include(x => x.UserRegistrations).Where(x => x.Id == player.UserId && x.UserRegistrations.Any(x => x.RegistrationId == 1));
      var userRegDate = userWithReg.First().UserRegistrations.First().RegistrationDate;

      var playerCompletions = _context.BcmPlayerGames
                                      .Include(x => x.Game)
                                      .Where(x => x.PlayerId == player.Id &&
                                        x.CompletionDate != null &&
                                        x.CompletionDate >= _bcmService.GetContestStartDate() &&
                                        x.CompletionDate >= userRegDate!.Value.AddDays(-1));

      var gamesCompletedThisYear = playerCompletions.ToList();

      var completedGamesCount = gamesCompletedThisYear.Count();
      var ratioOfGames = gamesCompletedThisYear.Select(x => x.Game!.SiteRatio);
      var estimateOfGames = gamesCompletedThisYear.Select(x => x.Game!.FullCompletionEstimate);

      playerBcmStats.Completions = completedGamesCount;
      playerBcmStats.AverageRatio = ratioOfGames.DefaultIfEmpty(0).Average();
      playerBcmStats.HighestRatio = ratioOfGames.DefaultIfEmpty(0).Max();
      playerBcmStats.AverageTimeEstimate = estimateOfGames.DefaultIfEmpty(0).Average();
      playerBcmStats.HighestTimeEstimate = estimateOfGames.DefaultIfEmpty(0).Max();

      double? basePoints = 0.0;
      foreach (var completion in gamesCompletedThisYear)
      {
        var pointValue = _bcmService.CalcBcmValue(completion.Platform, completion.Game!.SiteRatio, completion.Game.FullCompletionEstimate);
        if (pointValue != null)
          basePoints += pointValue;
      }

      playerBcmStats.BasePoints = basePoints;
      playerBcmStats.AveragePoints = completedGamesCount != 0 ? basePoints / completedGamesCount : 0;

      var rgscBonus = _statsService.ScoreRgscCompletions(player, gamesCompletedThisYear);
      var oddJobProgress = await _bcmService.GetOddJobChallengeProgress(player.Id);
      var oddJobBonus = oddJobProgress.Count() == 5 ? 1000 : 0;
      var playersChallenges = _context.PlayerYearlyChallenges.Include(x => x.YearlyChallenge)
                                .Where(x => x.PlayerId == player.Id && x.Approved);

      var communityStarBonus = playersChallenges.Where(x => x.YearlyChallenge!.Category == Data.YearlyCategory.CommunityStar).Count() == 20
                                  ? 5000 : 0;

      var tavisBonus = playersChallenges.Where(x => x.YearlyChallenge!.Category == Data.YearlyCategory.TheTAVIS).Count() == 20
                                  ? 5000 : 0;

      var retirementBonus = playersChallenges.Where(x => x.YearlyChallenge!.Category == Data.YearlyCategory.RetirementParty).Count() == 10
                                  ? 2500 : 0;

      var janBonus = _context.JanRecap.FirstOrDefault(x => x.PlayerId == player.Id)?.TotalPoints ?? 0;
      var febBonus = _context.FebRecap.FirstOrDefault(x => x.PlayerId == player.Id)?.TotalPoints ?? 0;
      var marBonus = _context.MarRecap.FirstOrDefault(x => x.PlayerId == player.Id)?.TotalPoints ?? 0;

      playerBcmStats.BonusPoints = rgscBonus + oddJobBonus + janBonus + febBonus + marBonus + tavisBonus + communityStarBonus + retirementBonus;
      playerBcmStats.TotalPoints = basePoints + playerBcmStats.BonusPoints;

      leaderboardList.Add(new Ranking
      {
        PlayerId = player.Id,
        BcmPoints = playerBcmStats.TotalPoints
      });
    }

    _context.SaveChanges();

    // after saving point calculations, lets order the leaderboard and save again for the rankings
    leaderboardList = leaderboardList.OrderByDescending(x => x.BcmPoints).ToList();

    foreach (var player in players)
    {
      var playerBcmStats = _context.BcmStats.First(x => x.PlayerId == player.Id);
      var previousRanking = playerBcmStats.Rank;
      var newRanking = leaderboardList.FindIndex(x => x.PlayerId == player.Id) + 1;

      playerBcmStats.Rank = newRanking;
      playerBcmStats.RankMovement = previousRanking - newRanking;
    }

    _context.SaveChanges();

    return Ok();
  }

  [HttpPost, Authorize(Roles = "Admin, Bcm Admin")]
  [Route("calcMonthlyBonusFeb")]
  public IActionResult CalcMonthlyBonusFeb()
  {
    var players = _bcmService.GetPlayers();
    var leaderboardList = new List<Ranking>();

    var test = new List<Game>();

    var allFebCompletions = _context.BcmPlayerGames
        .Include(x => x.Game)
        .Where(x => x.CompletionDate != null
            && x.CompletionDate.Value.Year == 2024
            && x.CompletionDate.Value.Month == 2)
        .ToList()
          .Where(x => !BcmRule.UpdateExclusions.Any(y => y.Id == x.GameId))
          .GroupBy(x => x.Game)
          .Select(g => Tuple.Create(g.Key, g.Count()))
          .ToList();

    var communityBonusReached = allFebCompletions.Where(x => x.Item2 > 10).Count() >= 3;

    _context.FebRecap.RemoveRange(_context.FebRecap.ToList());

    foreach (var player in players)
    {
      var userWithReg = _context.Users.Include(x => x.UserRegistrations).Where(x => x.Id == player.UserId && x.UserRegistrations.Any(x => x.RegistrationId == 1));
      var userRegDate = userWithReg.First().UserRegistrations.First().RegistrationDate;

      var playerCompletions = _context.BcmPlayerGames
                                      .Include(x => x.Game)
                                      .Where(x => x.PlayerId == player.Id &&
                                        x.CompletionDate != null &&
                                        x.CompletionDate >= _bcmService.GetContestStartDate() &&
                                        x.CompletionDate >= userRegDate!.Value.AddDays(-1) && 
                                        x.CompletionDate.Value.Year == 2024 &&
                                        x.CompletionDate.Value.Month == 2)
                                      .ToList();

      var gamesCompletedThisMonth = playerCompletions
              .Where(x =>
                  !BcmRule.UpdateExclusions.Any(y => y.Id == x.GameId) ||
                  !_context.MonthlyExclusions.Any(y => y.PlayerId == player.Id && y.GameId == x.GameId))
              .ToList();

      _statsService.CalcFebBonus(player, gamesCompletedThisMonth, allFebCompletions!, communityBonusReached);
    }

    foreach (var player in players)
    {
      var febStats = _context.FebRecap.FirstOrDefault(x => x.PlayerId == player.Id);
      if (febStats != null)
      {
        var febRanking = _context.FebRecap.OrderByDescending(x => x.TotalPoints).ToList();
        int rank = febRanking.FindIndex(x => x.Id == febStats.Id);
        febStats.Rank = rank + 1;
      }
    }

    _context.SaveChanges();

    return Ok();
  }

  [HttpPost, Authorize(Roles = "Admin, Bcm Admin")]
  [Route("calcMonthlyBonus")]
  public IActionResult CalcMonthlyBonusMar()
  {
    var players = _bcmService.GetPlayers();
    var leaderboardList = new List<Ranking>();

    var communityBonusReached = _statsService.CalcMarCommunityGoal();

    _context.MarRecap.RemoveRange(_context.MarRecap.ToList());
    _context.MonthlyExclusions.RemoveRange(_context.MonthlyExclusions.Where(x => x.Challenge == 3));

    foreach (var player in players)
    {
      var userWithReg = _context.Users.Include(x => x.UserRegistrations).Where(x => x.Id == player.UserId && x.UserRegistrations.Any(x => x.RegistrationId == 1));
      var userRegDate = userWithReg.First().UserRegistrations.First().RegistrationDate;

      var playerCompletions = _context.BcmPlayerGames
                                      .Include(x => x.Game)
                                      .Where(x => x.PlayerId == player.Id &&
                                        x.CompletionDate != null &&
                                        x.CompletionDate >= _bcmService.GetContestStartDate() &&
                                        x.CompletionDate >= userRegDate!.Value.AddDays(-1) &&
                                        x.CompletionDate!.Value.Year == 2024 &&
                                        x.CompletionDate!.Value.Month == 3).ToList();

      var gamesCompletedThisMonth = playerCompletions.Where(x => !BcmRule.UpdateExclusions.Any(y => y.Id == x.GameId)).ToList();

      _statsService.CalcMarBonus(player, gamesCompletedThisMonth, communityBonusReached);
    }

    foreach (var player in players)
    {
      var marStats = _context.MarRecap.FirstOrDefault(x => x.PlayerId == player.Id);
      if (marStats != null)
      {
        var marRanking = _context.MarRecap.OrderByDescending(x => x.TotalPoints).ToList();
        int rank = marRanking.FindIndex(x => x.Id == marStats.Id);
        marStats.Rank = rank + 1;
      }
    }

    _context.SaveChanges();

    return Ok();
  }

  //[HttpPost, Authorize(Roles = "Admin, Bcm Admin")]
  //[Route("calcMonthlyBonus3")]
  //public IActionResult CalcMonthlyBonus3()
  //{
  //  var players = _bcmService.GetPlayers();
  //  var leaderboardList = new List<Ranking>();

  //  var communityBonusReached = true;

  //  _context.JanRecap.RemoveRange(_context.JanRecap.ToList());

  //  foreach (var player in players)
  //  {
  //    var userWithReg = _context.Users.Include(x => x.UserRegistrations).Where(x => x.Id == player.UserId && x.UserRegistrations.Any(x => x.RegistrationId == 1));
  //    var userRegDate = userWithReg.First().UserRegistrations.First().RegistrationDate;

  //    var playerCompletions = _context.BcmPlayerGames
  //                                    .Include(x => x.Game)
  //                                    .Where(x => x.PlayerId == player.Id &&
  //                                      x.CompletionDate != null &&
  //                                      x.CompletionDate >= _bcmService.GetContestStartDate() &&
  //                                      x.CompletionDate >= userRegDate!.Value.AddDays(-1));

  //    var gamesCompletedThisMonth = playerCompletions.Where(x => x.CompletionDate!.Value.Year == 2024 && x.CompletionDate!.Value.Month == 1).ToList();

  //    _statsService.CalcJanBonus(player, gamesCompletedThisMonth, communityBonusReached);
  //  }

  //  foreach (var player in players)
  //  {
  //    var janStats = _context.JanRecap.FirstOrDefault(x => x.PlayerId == player.Id);
  //    if (janStats != null)
  //    {
  //      var janRanking = _context.JanRecap.OrderByDescending(x => x.TotalPoints).ToList();
  //      int rank = janRanking.FindIndex(x => x.Id == janStats.Id);
  //      janStats.Rank = rank + 1;
  //    }
  //  }

  //  _context.SaveChanges();

  //  return Ok();
  //}
}
