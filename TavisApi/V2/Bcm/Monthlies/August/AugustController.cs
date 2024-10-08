﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tavis.Extensions;
using Tavis.Models;
using TavisApi.ContestRules;
using TavisApi.Context;
using TavisApi.Services;
using TavisApi.V2.Bcm.Models;

namespace TavisApi.Controllers;

[Route("/v2/bcm/august")]
[ApiController]
public class AugustController : ControllerBase {
	private TavisContext _context;
	private readonly IUserService _userService;
	private readonly IBcmService _bcmService;
	private readonly IStatsService _statsService;

	public AugustController(TavisContext context, IUserService userService, IBcmService bcmService, IStatsService statsService)
	{
		_context = context;
		_userService = userService;
		_bcmService = bcmService;
		_statsService = statsService;
	}

	[HttpGet, Route("leaderboard")]
	public async Task<IActionResult> GetAugLeaderboard()
	{
		var recap = await _context.AugustRecap.ToListAsync();
		return Ok(recap);
	}

	[Authorize(Roles = "Guest")]
	[HttpGet, Route("monthly")]
	public async Task<IActionResult> AugustSummary(string player)
	{
		var localuser = _context.Users.FirstOrDefault(x => x.Gamertag == player);
		if (localuser is null) return BadRequest("Player not found with the provided gamertag");

		var bcmPlayer = _context.BcmPlayers.FirstOrDefault(x => x.UserId == localuser.Id);
		if (bcmPlayer is null) return BadRequest("BCM Player not found for the provided user");

		var recap = await _context.AugustRecap.FirstOrDefaultAsync(x => x.PlayerId == bcmPlayer.Id);
		var tributeCount = recap.DeathwatchTribute ? 1 : 0;
		tributeCount += recap.ImperialFistTribute ? 1 : 0;
		tributeCount += recap.UltramarinesTribute ? 1 : 0;
		tributeCount += recap.BloodAngelTribute ? 1 : 0;
		tributeCount += recap.SpaceWolvesTribute ? 1 : 0;

		var chaos = await _context.AugustRecap.Where(x => x.PlayerId == 33 || x.PlayerId == 8).ToListAsync();
		var chaosCount = chaos.Sum(x => x.AchievementCount);

		return Ok(new {
			recap.AchievementCount,
			recap.CommunityBonus,
			recap.TotalPoints,
			recap.Participation,
			tributeCount,
			chaosCount,
		});
	}

	[HttpPost, Authorize(Roles = "Admin, Bcm Admin")]
	[Route("calc")]
	public IActionResult Calc()
	{
		var players = _bcmService.GetPlayers();
		var leaderboardList = new List<Ranking>();

		_context.AugustRecap.RemoveRange(_context.AugustRecap.ToList());
		_context.MonthlyExclusions.RemoveRange(_context.MonthlyExclusions.Where(x => x.Challenge == 8));

		var ironsGames = _context.BcmPlayerGames
														.Include(x => x.Game)
														.Where(x => x.PlayerId == 12 && x.CompletionDate != null)
														.ToList();

		foreach (var player in players) {
			var userWithReg = _context.Users.Include(x => x.UserRegistrations).Where(x => x.Id == player.UserId && x.UserRegistrations.Any(x => x.RegistrationId == 1));
			var userRegDate = userWithReg.First().UserRegistrations.First().RegistrationDate;

			var playerCompletions = _context.BcmPlayerGames
																			.Include(x => x.Game)
																			.Where(x => x.PlayerId == player.Id &&
																				x.CompletionDate != null &&
																				x.CompletionDate >= _bcmService.GetContestStartDate() &&
																				x.CompletionDate >= userRegDate!.Value.AddDays(-1) &&
																				x.CompletionDate!.Value.Year == 2024 &&
																				x.CompletionDate!.Value.Month == 8)
																			.AsEnumerable()
																			.Where(x => Queries.FilterGamesForYearlies(x.Game!, x))
																			.ToList();

			var gamesCompletedThisMonth = playerCompletions.Where(x => !BcmRule.UpdateExclusions.Any(y => y.Id == x.GameId)
																														&& !_context.MonthlyExclusions.Any(y => y.PlayerId == player.Id && y.GameId == x.GameId)).ToList();

			CalcAugustBonus(player, gamesCompletedThisMonth, ironsGames);
		}

		var communityProgress = CalcAugustCommunityProgress();

		foreach (var player in players) {
			var stats = _context.AugustRecap.FirstOrDefault(x => x.PlayerId == player.Id);
			if (stats != null) {
				if (communityProgress >= 40000 && stats.Participation) stats.TotalPoints += 1000;

				var ranking = _context.AugustRecap.OrderByDescending(x => x.TotalPoints).ToList();
				int rank = ranking.FindIndex(x => x.Id == stats.Id);
				stats.Rank = rank + 1;

				stats.CommunityBonus = communityProgress;
			}
		}

		_context.SaveChanges();

		return Ok();
	}

	private int CalcAugustCommunityProgress()
	{
		var nothsGames = _context.BcmPlayerGames
																	.Include(x => x.Game)
																	.Where(x => x.PlayerId == 33 &&
																		x.CompletionDate != null &&
																		x.CompletionDate >= _bcmService.GetContestStartDate() &&
																		x.CompletionDate!.Value.Year == 2024 &&
																		x.CompletionDate!.Value.Month == 8 &&
																		x.Game != null && x.Game.Gamerscore > 1000)
																	.AsEnumerable()
																	.Where(x => Queries.FilterGamesForYearlies(x.Game!, x))
																	.ToList();

		var emzGames = _context.BcmPlayerGames
																	.Include(x => x.Game)
																	.Where(x => x.PlayerId == 8 &&
																		x.CompletionDate != null &&
																		x.CompletionDate >= _bcmService.GetContestStartDate() &&
																		x.CompletionDate!.Value.Year == 2024 &&
																		x.CompletionDate!.Value.Month == 8 &&
																		x.Game != null && x.Game.Gamerscore > 1000)
																	.AsEnumerable()
																	.Where(x => Queries.FilterGamesForYearlies(x.Game!, x))
																	.ToList();

		var communityGames = _context.BcmPlayerGames
																	.Include(x => x.Game)
																	.Where(x => x.PlayerId != 8 && x.PlayerId != 33 &&
																		x.CompletionDate != null &&
																		x.CompletionDate >= _bcmService.GetContestStartDate() &&
																		x.CompletionDate!.Value.Year == 2024 &&
																		x.CompletionDate!.Value.Month == 8 &&
																		x.Game != null && x.Game.Gamerscore > 1000)
																	.AsEnumerable()
																	.Where(x => Queries.FilterGamesForYearlies(x.Game!, x))
																	.ToList();

		var halfCommunityGames = _context.BcmPlayerGames
																	.Include(x => x.Game)
																	.Where(x => x.PlayerId != 8 && x.PlayerId != 33 &&
																		x.CompletionDate != null &&
																		x.CompletionDate >= _bcmService.GetContestStartDate() &&
																		x.CompletionDate!.Value.Year == 2024 &&
																		x.CompletionDate!.Value.Month == 8 &&
																		x.Game != null && x.Game.Gamerscore > 1000)
																	.AsEnumerable()
																	.Where(x => Queries.FilterGamesForYearlies(x.Game!, x))
																	.ToList()
																	.Sum(x => x.AchievementCount ?? 0) * .5;

		var warhammerBonus = communityGames.Where(x => WarhammerGameIds.Contains((int)x.GameId!)).Sum(x => x.AchievementCount) * 9;

		var evilCount = nothsGames.Sum(x => x.AchievementCount) + emzGames.Sum(x => x.AchievementCount) ?? 0;
		var goodCount = communityGames.Sum(x => x.AchievementCount) ?? 0;
		goodCount += warhammerBonus ?? 0;
		goodCount += (int)Math.Floor(halfCommunityGames);

		return goodCount - evilCount;
	}

	private void CalcAugustBonus(BcmPlayer player, List<BcmPlayerGame> games, List<BcmPlayerGame> ironsGames)
	{
		var qualifiedCompletions = games.Where(x => x.Game != null && x.Game.Gamerscore > 1000);

		var participated = qualifiedCompletions.Count() > 0;
		var baTribute = qualifiedCompletions.Any(x => x.Platform == V2.TrueAchievements.Models.Platform.Xbox360) ? .05 : 0;
		var ifTribute = qualifiedCompletions.Any(x => ironsGames.Select(x => x.GameId).Contains(x.GameId)) ? .05 : 0;

		var requiredChars = new List<char> { 'w', 'o', 'o', 'f' };

		foreach (var game in qualifiedCompletions) {
			if (game.Game == null) continue;

			var unwantedStrings = new[] { "(Windows)", "(Xbox 360)", "(Nintendo Switch)", "(Android)" };

			var title = game.Game.Title;

			// Remove unwanted strings
			foreach (var unwanted in unwantedStrings) {
				title = title.Replace(unwanted, string.Empty);
			}

			// Convert to lowercase and then to a list
			var test = title.ToLower().ToList();

			foreach (var c in requiredChars.ToList()) {
				if (test.Contains(c)) {
					requiredChars.Remove(c);
					test.Remove(c);
				}
			}

			if (!requiredChars.Any()) break;
		}

		var swTribute = !requiredChars.Any() ? 0.05 : 0;


		var umTribute = qualifiedCompletions.Any(x => x.Game != null && x.Game.SiteRating >= 4.25 && x.Game.GamersWithGame >= 2000) ? .05 : 0;
		var dwTribute = qualifiedCompletions.Any(x => x.Game != null && x.Game.FullCompletionEstimate >= 80) ? 0.05 : 0;

		var tributes = baTribute + ifTribute + swTribute + umTribute + dwTribute;

		var warhammerBonus = qualifiedCompletions.Where(x => WarhammerGameIds.Contains((int)x.GameId!)).Sum(x => x.AchievementCount) * 9;

		var totalPoints = 0;

		foreach (var completion in qualifiedCompletions) {
			var completionValue = _bcmService.CalcBcmValue(completion.Platform, completion.Game!.SiteRatio, completion.Game!.FullCompletionEstimate) ?? 0;

			if (completion.Game.Gamerscore >= 1750) {
				var individualBonusPoints = (int)Math.Floor(completionValue * (.60 + tributes));
				completion.BcmPoints = individualBonusPoints;
				totalPoints += individualBonusPoints;
			}
			else if (completion.Game.Gamerscore >= 1500) {
				var individualBonusPoints = (int)Math.Floor(completionValue * (.40 + tributes));
				completion.BcmPoints = individualBonusPoints;
				totalPoints += individualBonusPoints;
			}
			else if (completion.Game.Gamerscore > 1000) {
				var individualBonusPoints = (int)Math.Floor(completionValue * (.20 + tributes));
				completion.BcmPoints = individualBonusPoints;
				totalPoints += individualBonusPoints;
			}

			_context.MonthlyExclusions.Add(new MonthlyExclusion {
				Challenge = 8,
				GameId = completion.GameId,
				PlayerId = player.Id
			});
		}

		_context.AugustRecap.Add(new AugRecap {
			PlayerId = player.Id,
			Gamertag = player.User!.Gamertag!,
			AchievementCount = qualifiedCompletions.Sum(x => x.AchievementCount) + warhammerBonus,
			BloodAngelTribute = baTribute != 0,
			ImperialFistTribute = ifTribute != 0,
			SpaceWolvesTribute = swTribute != 0,
			UltramarinesTribute = umTribute != 0,
			DeathwatchTribute = dwTribute != 0,
			Participation = participated,
			TotalPoints = totalPoints,
		});

		_context.SaveChanges();
	}

	private List<int> WarhammerGameIds = new() {
		1430, 1282, 378, 5047, 398, 7796, 672, 1193, 1386, 9742, 5387, 1387, 4725, 374, 9853, 1337, 5457, 1258, 4434, 3165, 10055, 4640, 8322, 403, 1392, 8357,
		8341, 8233, 752, 345, 6571, 1411, 1369, 1158, 8334, 897, 788
	};
}