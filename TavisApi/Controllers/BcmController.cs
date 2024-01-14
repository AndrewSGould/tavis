namespace WebApi.Controllers;

using TavisApi.ContestRules;
using TavisApi.Context;
using TavisApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Tavis.Models;
using System.Data;
using DocumentFormat.OpenXml;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Tavis.Extensions;

[ApiController]
[Route("[controller]")]
public class BcmController : ControllerBase
{
  private TavisContext _context;
  private readonly IParser _parser;
  private readonly IDataSync _dataSync;
  private readonly IBcmService _bcmService;
  private readonly IUserService _userService;
  private readonly IDiscordService _discordService;
  private static readonly Random rand = new Random();

  public BcmController(TavisContext context, IParser parser, IDataSync dataSync, IBcmService bcmService, IUserService userService, IDiscordService discordService)
  {
    _context = context;
    _parser = parser;
    _dataSync = dataSync;
    _bcmService = bcmService;
    _userService = userService;
    _discordService = discordService;
  }

  [HttpGet, Authorize(Roles = "Guest")]
  [Route("getPlayerList")]
  public IActionResult GetPlayerList()
  {
    return Ok(_bcmService.GetPlayers().Select(x => x.User!.Gamertag).ToList());
  }

  [HttpGet]
  [Route("getBcmPlayer")]
  public IActionResult BcmPlayer(string player)
  {
    var localuser = _context.Users.FirstOrDefault(x => x.Gamertag == player);

    if (localuser is null) return BadRequest("No gamertag found with provided player");

    var bcmPlayer = _context.BcmPlayers.First(x => x.UserId == localuser.Id);

    if (bcmPlayer == null) return BadRequest("Player not found");

    var playerBcmStats = _context.BcmStats?.FirstOrDefault(x => x.PlayerId == bcmPlayer.Id);

    return Ok(bcmPlayer);
  }

  [HttpGet]
  [Route("getBcmPlayerWithGames")]
  public IActionResult BcmPlayerWithGames(string player)
  {
    var localuser = _context.Users.Include(x => x.UserRegistrations).FirstOrDefault(x => x.Gamertag == player);

    if (localuser is null) return BadRequest("No gamertag found with provided player");

    var bcmPlayer = _context.BcmPlayers.First(x => x.UserId == localuser.Id);

    if (bcmPlayer == null) return BadRequest("Player not found");

    var registrations = _context.Registrations
        .Include(x => x.UserRegistrations)
        .Where(x => x.UserRegistrations.Any(ur => ur.UserId == localuser.Id))
        .ToList();

    var bcmRegDate = registrations.First(x => x.Id == 1).StartDate;

    var userRegDate = localuser.UserRegistrations.First(x => x.RegistrationId == 1).RegistrationDate; // TODO: BCM

    var playerBcmGames = _context.BcmPlayerGames.Include(x => x.Game).Where(x => x.BcmPlayer == bcmPlayer
                                                                                && x.CompletionDate != null
                                                                                && x.CompletionDate > userRegDate
                                                                                && x.CompletionDate > bcmRegDate);

    var pointedGames = new List<object>();

    foreach (var game in playerBcmGames)
    {
      var newGame = new
      {
        Game = game,
        Points = _bcmService.CalcBcmValue(game.Platform, game.Game.SiteRatio, game.Game.FullCompletionEstimate),
      };

      pointedGames.Add(newGame);
    }

    var avgRatio = playerBcmGames
        .Where(x => x.CompletionDate != null && x.CompletionDate > userRegDate && x.CompletionDate > bcmRegDate)
        .Select(x => x.Game.SiteRatio)
        .AsEnumerable()
        .DefaultIfEmpty(0)
        .Average();
    var avgTime = playerBcmGames
        .Where(x => x.CompletionDate != null && x.CompletionDate > userRegDate && x.CompletionDate > bcmRegDate)
        .Select(x => x.Game.FullCompletionEstimate)
        .AsEnumerable()
        .DefaultIfEmpty(0)
        .Average();
    var highestTime = playerBcmGames
        .Where(x => x.CompletionDate != null && x.CompletionDate > userRegDate && x.CompletionDate > bcmRegDate)
        .Select(x => x.Game.FullCompletionEstimate)
        .AsEnumerable()
        .DefaultIfEmpty(0)
        .Max();
    var highestRatio = playerBcmGames
        .Where(x => x.CompletionDate != null && x.CompletionDate > userRegDate && x.CompletionDate > bcmRegDate)
        .Select(x => x.Game.SiteRatio)
        .AsEnumerable()
        .DefaultIfEmpty(0)
        .Max();

    return Ok(new
    {
      Player = bcmPlayer,
      Games = pointedGames,
      AvgRatio = avgRatio,
      AvgTime = avgTime,
      HighestTime = highestTime,
      HighestRatio = highestRatio
    });
  }

  [HttpGet]
  [Route("player/abcSummary")]
  public async Task<IActionResult> GetPlayerAbcSummary(string player)
  {
    var localuser = _context.Users.FirstOrDefault(x => x.Gamertag == player);

    if (localuser is null) return BadRequest("Player not found with the provided gamertag");

    var bcmPlayer = _context.BcmPlayers.FirstOrDefault(x => x.UserId == localuser.Id);

    if (bcmPlayer is null) return BadRequest("BCM Player not found for the provided user");

    return Ok(await _bcmService.GetAlphabetChallengeProgress(bcmPlayer.Id));
  }

  [HttpGet]
  [Route("player/oddjobSummary")]
  public async Task<IActionResult> GetPlayerOddjobSummary(string player)
  {
    var localuser = _context.Users.FirstOrDefault(x => x.Gamertag == player);

    if (localuser is null) return BadRequest("Player not found with the provided gamertag");

    var bcmPlayer = _context.BcmPlayers.FirstOrDefault(x => x.UserId == localuser.Id);

    if (bcmPlayer is null) return BadRequest("BCM Player not found for the provided user");

    return Ok(await _bcmService.GetOddJobChallengeProgress(bcmPlayer.Id));
  }

  [HttpGet]
  [Route("player/miscstats")]
  public async Task<IActionResult> GetPlayerMiscStats(string player)
  {
    var localuser = _context.Users.FirstOrDefault(x => x.Gamertag == player);

    if (localuser is null) return BadRequest("Player not found with the provided gamertag");

    var bcmPlayer = _context.BcmPlayers.FirstOrDefault(x => x.UserId == localuser.Id);

    if (bcmPlayer is null) return BadRequest("BCM Player not found for the provided user");

    return Ok();
  }

  public class RandomRoll
  {
    public string? selectedPlayer { get; set; }
    public int? selectedGameId { get; set; }
  }

  [HttpPost, Authorize(Roles = "Admin")]
  [Route("rollRandom")]
  public IActionResult RollRandomGame([FromBody] RandomRoll roll)
  {
    var players = _context.BcmPlayers.Include(u => u.User).Include(x => x.BcmRgscs).ToList();
    var currentBcmPlayer = players.FirstOrDefault(x => x.User!.Gamertag == roll.selectedPlayer);

    if (currentBcmPlayer is null)
    {
      players = players.Where(x => x.BcmRgscs == null || x.BcmRgscs.Count() == 0 || x.BcmRgscs
                        .OrderByDescending(x => x.Issued)
                        .First().Issued <= DateTime.UtcNow.AddDays(-25))
                        .ToList();

      if (players.Count() < 1) return BadRequest("no users left to random");

      var playerIndex = new Random().Next(0, players.Count);
      currentBcmPlayer = players[playerIndex];
    }

    _context.Attach(currentBcmPlayer);

    var randomGameOptions = _context.BcmPlayerGames?
            .Join(_context.Games!, pg => pg.GameId,
              g => g.Id, (pg, g) => new { BcmPlayersGames = pg, Games = g })
            .Where(x => x.BcmPlayersGames.PlayerId == currentBcmPlayer.Id
              && x.Games.GamersCompleted > 0
              && x.Games.FullCompletionEstimate <= BcmRule.RandomMaxEstimate
              && !x.Games.Unobtainables
              && !x.BcmPlayersGames.NotForContests
              && x.BcmPlayersGames.CompletionDate == null
              && x.BcmPlayersGames.Ownership != Ownership.NoLongerHave
              && BcmRule.RandomValidPlatforms.Contains(x.BcmPlayersGames.Platform!))
            .AsEnumerable() // TODO: rewrite so this stays as a query?
            .Where(x => Queries.FilterGamesForYearlies(x.Games, x.BcmPlayersGames))
            .ToList();

    var currentRandoms = _context.BcmRgsc.Where(x => !x.Rerolled && x.BcmPlayerId == currentBcmPlayer.Id);

    randomGameOptions = randomGameOptions?
      .Where(x => !currentRandoms.Any(y => y.GameId == x.Games.Id))
      .ToList();

    // if we get a game, they are rerolling an old game
    var rolledRandom = currentRandoms.FirstOrDefault(x => x.GameId == roll.selectedGameId);

    if (roll.selectedGameId != -1 && rolledRandom is not null)
    {
      rolledRandom.Rerolled = true;
      rolledRandom.RerollDate = DateTime.UtcNow;
    }

    var currentChallenge = currentRandoms.OrderByDescending(x => x.Challenge).Select(x => x.Challenge).FirstOrDefault();
    var nextChallenge = 1;

    if (currentChallenge.HasValue)
      nextChallenge = currentChallenge.Value + 1;

    if (randomGameOptions is null || randomGameOptions?.Count() < 50)
    {
      if (roll.selectedGameId == -1 && rolledRandom is null)
      {
        // they are rerolling an invalid game, but it's still not valid
        var mostRecentRandom = currentRandoms.OrderByDescending(x => x.Challenge).First();
        mostRecentRandom.Issued = DateTime.UtcNow;
        mostRecentRandom.PoolSize = randomGameOptions?.Count() ?? 0;
      }
      else
      {
        _context.BcmRgsc.Add(new BcmRgsc
        {
          Issued = DateTime.UtcNow,
          GameId = null,
          BcmPlayerId = currentBcmPlayer.Id,
          PreviousGameId = roll.selectedGameId != -1 && roll.selectedGameId != null ? rolledRandom!.GameId : null,
          Challenge = roll.selectedGameId != -1 && roll.selectedGameId != null ? rolledRandom!.Challenge : nextChallenge,
          PoolSize = randomGameOptions?.Count() ?? 0
        });
      }

      _context.SaveChanges();

      return Ok(new { PoolSize = randomGameOptions?.Count() ?? 0, currentBcmPlayer.User });
    }

    var randomIndex = new Random().Next(0, randomGameOptions!.Count);
    var currentRandom = randomGameOptions[randomIndex];

    _context.BcmRgsc.Add(new BcmRgsc
    {
      Issued = DateTime.UtcNow,
      GameId = currentRandom.Games.Id,
      BcmPlayerId = currentBcmPlayer.Id,
      Challenge = roll.selectedGameId != -1 && roll.selectedGameId != null ? rolledRandom!.Challenge : nextChallenge,
      PreviousGameId = roll.selectedGameId != -1 && roll.selectedGameId != null ? rolledRandom!.GameId : null,
      PoolSize = randomGameOptions?.Count() ?? 0
    });

    _context.SaveChanges();

    return Ok(new
    {
      PoolSize = randomGameOptions?.Count() ?? 0,
      currentBcmPlayer.User,
      Result = currentRandom,
      BcmValue = _bcmService.CalcBcmValue(currentRandom.BcmPlayersGames.Platform, currentRandom.Games.SiteRatio, currentRandom.Games.FullCompletionEstimate)
    });
  }

  [Authorize(Roles = "Guest")]
  [HttpGet, Route("monthly/jan")]
  public async Task<IActionResult> JanSummary(string player)
  {
    var localuser = _context.Users.FirstOrDefault(x => x.Gamertag == player);
    if (localuser is null) return BadRequest("Player not found with the provided gamertag");

    var bcmPlayer = _context.BcmPlayers.FirstOrDefault(x => x.UserId == localuser.Id);
    if (bcmPlayer is null) return BadRequest("BCM Player not found for the provided user");    

    return Ok(await _context.BcmMonthlyStats.FirstOrDefaultAsync(x => x.BcmPlayerId == bcmPlayer.Id && x.Challenge == 1));
  }

  [Authorize(Roles = "Guest")]
  [HttpPost, Route("registerUser")]
  public async Task<IActionResult> RegisterUser()
  {
    try
    {
      User? user = _userService.GetCurrentUser();
      if (user is null) return BadRequest("Could not determine user");

      var bcmReg = _context.Registrations.Find(_bcmService.GetRegistrationId()) ?? throw new Exception("Unable to get Registration ID for BCM");

      user.UserRegistrations.Add(new UserRegistration { Registration = bcmReg, RegistrationDate = DateTime.UtcNow });

      _context.BcmPlayers.Add(new BcmPlayer
      {
        UserId = user.Id,
      });

      _context.SaveChanges();

      try
      {
        await _discordService.AddBcmParticipantRole(user);
        var userInfo = _context.Users.Include(x => x.UserRegistrations)
                                  .FirstOrDefault(x => x.UserRegistrations.Any(x => x.User == user && x.Registration.Name == "Better Completions Matter"));

        return Ok(new { RegDate = userInfo?.UserRegistrations.FirstOrDefault()?.RegistrationDate });
      }
      catch
      {
        return BadRequest("Something went wrong trying to register for BCM");
      }
    }
    catch (Exception ex)
    {
      return BadRequest(ex.Message);
    }
  }

  [Authorize(Roles = "Participant")]
  [HttpPost, Route("unregisterUser")]
  public async Task<IActionResult> UnregisterUser()
  {
    throw new NotImplementedException();
  }

  [Authorize(Roles = "Participant")]
  [HttpGet, Route("getPlayersGenres")]
  public async Task<IActionResult> GetPlayersGenres(string player)
  {
    var localuser = _context.Users.Include(x => x.BcmPlayer).FirstOrDefault(x => x.Gamertag == player);
    if (localuser is null) return BadRequest("No user found with supplied gamertag");

    var playerId = localuser.BcmPlayer?.Id;
    if (playerId is null) return BadRequest("Could not get Bcm Player");

    var pgs = _context.BcmPlayerGames.Include(x => x.Game).Where(x => x.BcmPlayer == localuser.BcmPlayer && x.CompletionDate != null);

    var genreStats = await _context.Genres
      .GroupJoin(
          _context.GameGenres,
          genre => genre.Id,
          gameGenre => gameGenre.GenreId,
          (genre, gameGenres) => new
          {
            GenreId = genre.Id,
            GenreName = genre.Name,
            GenreCount = gameGenres
            .Join(
                _context.BcmPlayerGames
                    .Where(bpg => bpg.PlayerId == playerId && bpg.CompletionDate != null),
                gg => gg.GameId,
                bpg => bpg.GameId,
                (gg, bpg) => gg // Use gg instead of 1
            )
            .Count()
          }
      )
      .OrderByDescending(result => result.GenreCount)
      .Select(x => new
      {
        Name = x.GenreName,
        Value = x.GenreCount
      })
      .ToListAsync();

    return Ok(genreStats);
  }
}
